using System.Net;
using System.Reflection;
using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.Extensions.DependencyInjection;

namespace Dbvprovas.Api.Tests.Tenancy;

public sealed class EndpointPolicyTests(PostgresFixture db)
{
    // Cada rota anônima da API vale só com o próprio método: o mesmo caminho com outro método não entra.
    private static readonly string[] AnonymousApiRoutes =
    [
        "GET /api/dev/accounts",
        "POST /api/dev/sessions",
        "GET /health/live",
        "GET /health/ready",
        "GET /openapi/{documentName}.json",
    ];

    // O site e os arquivos estáticos são só leitura.
    private static readonly string[] SiteMethods = ["GET", "HEAD"];

    // Páginas do site: a rota exata de cada componente com @page da biblioteca de telas (D-112, CA-TEN-006). Página nova
    // entra sozinha, sem prefixo, e só vale com o marcador de componente do MapRazorComponents.
    private static readonly string[] SitePageRoutes = typeof(Dbvprovas.Ui.Routes).Assembly.GetExportedTypes()
        .SelectMany(type => type.GetCustomAttributes<RouteAttribute>(inherit: false))
        .Select(route => "/" + route.Template.TrimStart('/'))
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    // As páginas e o endpoint de redirecionamento do Blazor (D-112), que não tem marcador próprio e fica pela rota exata.
    // Os arquivos estáticos entram pelo marcador do MapStaticAssets e a lista de recursos pelo mapa de importação das
    // páginas; nunca por prefixo de caminho.
    private static readonly string[] AnonymousSiteRoutes = [.. SitePageRoutes, "/_framework/opaque-redirect"];

    private static readonly string[] ResourceCollectionNames =
    [
        "_framework/resource-collection.js",
        "_framework/resource-collection.js.gz",
    ];

    // O AllowAnonymous do site vale em qualquer ambiente, então a varredura roda nos dois.
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task CA_TEN_006_Every_endpoint_has_explicit_policy(string environment)
    {
        await using var api = new ApiFactory(db, environment);
        var endpoints = RouteEndpoints(api);

        Assert.NotEmpty(endpoints);
        var allowed = AnonymousRoutes(endpoints);
        foreach (var endpoint in endpoints)
            Assert.True(HasExplicitPolicy(endpoint, allowed),
                $"Endpoint without explicit policy: {Methods(endpoint)} {endpoint.RoutePattern.RawText}");
    }

    // As rotas de página derivadas são exatas e ficam fora da API, da saúde e dos arquivos do framework (CA-TEN-006).
    [Fact]
    public void CA_TEN_006_Site_page_routes_are_exact_and_outside_api()
    {
        Assert.Contains("/", SitePageRoutes);
        Assert.Contains("/club", SitePageRoutes);
        Assert.Contains("/not-found", SitePageRoutes);
        foreach (var route in SitePageRoutes)
            Assert.True(IsExactPageRoute(route), route);
    }

    [Theory]
    [InlineData("/{slug}", false)]
    [InlineData("/{a}/{b}", false)]
    [InlineData("/{a}/x", false)]
    [InlineData("/api/x", false)]
    [InlineData("/_framework/x", false)]
    [InlineData("/club/*", false)]
    [InlineData("/", true)]
    [InlineData("/club", true)]
    [InlineData("/club/members/{membershipId:guid}", true)]
    [InlineData("admin/clubs/{clubId:guid}/director", true)]
    public void CA_TEN_006_Page_route_shape_is_checked(string route, bool exact)
    {
        Assert.Equal(exact, IsExactPageRoute(route));
    }

    // Página anônima só com o primeiro segmento literal (ou a raiz), sem curinga e fora da API, da saúde e dos arquivos do
    // framework: um parâmetro no começo atenderia qualquer caminho, inclusive /api/... desconhecido, que é o 404 da API
    // (CA-TEN-006, D-112).
    private static bool IsExactPageRoute(string route)
    {
        var path = "/" + route.TrimStart('/');
        if (path.Contains('*'))
            return false;
        foreach (var prefix in new[] { "/api", "/health", "/openapi", "/_framework", "/_content" })
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
        var segments = RoutePatternFactory.Parse(path).PathSegments;
        return segments.Count == 0 || segments[0].Parts.All(part => part is RoutePatternLiteralPart);
    }

    [Fact]
    public void CA_TEN_006_Anonymous_endpoint_outside_allowlist_is_rejected()
    {
        Assert.False(HasExplicitPolicy(AnonymousEndpoint("/export"), FixedRoutes()));
    }

    // Sem método declarado, o endpoint atende a qualquer método; anônimo assim não entra, nem numa rota da lista.
    [Fact]
    public void CA_TEN_006_Anonymous_endpoint_without_declared_methods_is_rejected()
    {
        var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/health/live"), 0);
        builder.Metadata.Add(new AllowAnonymousAttribute());
        Assert.False(HasExplicitPolicy((RouteEndpoint)builder.Build(), FixedRoutes()));
    }

    // A lista da API é de pares (método, rota), e o site só aceita leitura.
    [Theory]
    [InlineData("GET", "/health/live", true)]
    [InlineData("POST", "/health/live", false)]
    [InlineData("POST", "/api/dev/sessions", true)]
    [InlineData("GET", "/api/dev/sessions", false)]
    [InlineData("POST", "/club", false)]
    public void CA_TEN_006_Anonymous_route_is_allowed_only_with_its_method(string method, string route, bool allowed)
    {
        var endpoint = AnonymousEndpoint(route, new HttpMethodMetadata([method]), new ComponentTypeMetadata(typeof(object)));
        Assert.Equal(allowed, HasExplicitPolicy(endpoint, FixedRoutes()));
    }

    // O site só abre exceções por rota exata: nada sob /_framework, /_content ou /club entra por prefixo.
    [Theory]
    [InlineData("/_framework/export")]
    [InlineData("/_content/Dbvprovas.Ui/export")]
    [InlineData("/club/export")]
    [InlineData("/not-found/export")]
    public void CA_TEN_006_Anonymous_site_lookalike_is_rejected(string route)
    {
        Assert.False(HasExplicitPolicy(AnonymousEndpoint(route), FixedRoutes()));
    }

    // A rota de página só vale para o endpoint que o MapRazorComponents cria (marcador do componente):
    // um endpoint anônimo de API mapeado em /, /club ou /not-found não herda a exceção (CA-TEN-006, D-112).
    [Theory]
    [InlineData("/")]
    [InlineData("/club")]
    [InlineData("/not-found")]
    public void CA_TEN_006_Anonymous_endpoint_at_page_route_without_component_marker_is_rejected(string route)
    {
        Assert.False(HasExplicitPolicy(AnonymousEndpoint(route), FixedRoutes()));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/club")]
    [InlineData("/not-found")]
    public void CA_TEN_006_Anonymous_endpoint_at_page_route_with_component_marker_is_accepted(string route)
    {
        Assert.True(HasExplicitPolicy(AnonymousEndpoint(route, new ComponentTypeMetadata(typeof(object))), FixedRoutes()));
    }

    // O marcador do componente não abre rota nova: ela continua precisando estar na lista.
    [Fact]
    public void CA_TEN_006_Component_marker_does_not_allow_a_route_outside_the_allowlist()
    {
        Assert.False(HasExplicitPolicy(AnonymousEndpoint("/export", new ComponentTypeMetadata(typeof(object))), FixedRoutes()));
    }

    // O marcador de arquivo estático vale só quando a rota do descritor é a do endpoint.
    [Fact]
    public void CA_TEN_006_Static_asset_marker_must_match_the_endpoint_route()
    {
        var matching = AnonymousEndpoint("/_framework/export", new StaticAssetDescriptor { Route = "_framework/export", AssetPath = "export" });
        var mismatched = AnonymousEndpoint("/_framework/export", new StaticAssetDescriptor { Route = "app.css", AssetPath = "app.css" });

        Assert.True(HasExplicitPolicy(matching, FixedRoutes()));
        Assert.False(HasExplicitPolicy(mismatched, FixedRoutes()));
    }

    // A lista de recursos do Blazor tem nome com impressão digital; só os nomes que as páginas declaram
    // no mapa de importação são aceitos.
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task CA_TEN_006_Resource_collection_routes_come_from_the_page_import_map(string environment)
    {
        await using var api = new ApiFactory(db, environment);
        var endpoints = RouteEndpoints(api);
        var allowed = AnonymousRoutes(endpoints);

        var declared = allowed.Where(route => route.StartsWith("/_framework/resource-collection", StringComparison.Ordinal)).ToList();

        Assert.Equal(4, declared.Count);
        Assert.False(HasExplicitPolicy(AnonymousEndpoint("/_framework/resource-collection.zzzzz.js"), allowed));
    }

    // O conteúdo do mapa de importação nunca alarga a lista: valor fora da forma do arquivo de recursos é ignorado.
    [Theory]
    [InlineData("./_framework/export")]
    [InlineData("./_framework/resource-collection.abc12.exe")]
    [InlineData("./_framework/resource-collection.x/export.js")]
    [InlineData("./_content/Dbvprovas.Ui/app.css")]
    [InlineData("/_framework/resource-collection.abc12.js")]
    [InlineData("_framework/resource-collection.abc12.js")]
    public void CA_TEN_006_Import_map_value_outside_resource_collection_shape_is_ignored(string value)
    {
        var page = AnonymousEndpoint("/", ImportMap(("_framework/resource-collection.js", value)));

        var routes = AnonymousRoutes([page]);

        Assert.True(routes.SetEquals(FixedRoutes()));
    }

    [Fact]
    public void CA_TEN_006_Import_map_value_with_resource_collection_shape_is_accepted()
    {
        var page = AnonymousEndpoint("/", ImportMap(
            ("_framework/resource-collection.js", "./_framework/resource-collection.abc12.js"),
            ("_framework/resource-collection.js.gz", "./_framework/resource-collection.abc12.js.gz")));

        var routes = AnonymousRoutes([page]);

        Assert.Contains("/_framework/resource-collection.js", routes);
        Assert.Contains("/_framework/resource-collection.js.gz", routes);
        Assert.Contains("/_framework/resource-collection.abc12.js", routes);
        Assert.Contains("/_framework/resource-collection.abc12.js.gz", routes);
    }

    private static ImportMapDefinition ImportMap(params (string Name, string Value)[] imports) =>
        new(imports.ToDictionary(import => import.Name, import => import.Value), scopes: null, integrity: null);

    private static List<RouteEndpoint> RouteEndpoints(ApiFactory api) =>
        api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

    private static string Route(RouteEndpoint endpoint) => "/" + endpoint.RoutePattern.RawText!.TrimStart('/');

    private static string Methods(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods is { Count: > 0 } methods ? string.Join(',', methods) : "*";

    // As rotas anônimas do site; as da API são conferidas com o método, em AnonymousApiRoutes.
    private static HashSet<string> FixedRoutes() => [.. AnonymousSiteRoutes];

    private static HashSet<string> AnonymousRoutes(IEnumerable<RouteEndpoint> endpoints)
    {
        var routes = FixedRoutes();
        var siteImports = endpoints
            .Where(endpoint => AnonymousSiteRoutes.Contains(Route(endpoint)))
            .Select(endpoint => endpoint.Metadata.GetMetadata<ImportMapDefinition>()?.Imports)
            .OfType<IReadOnlyDictionary<string, string>>();
        foreach (var imports in siteImports)
        {
            foreach (var name in ResourceCollectionNames)
            {
                if (!imports.TryGetValue(name, out var fingerprinted) || !IsResourceCollectionFile(fingerprinted))
                    continue;
                routes.Add("/" + name);
                routes.Add(fingerprinted[1..]); // "./_framework/..." vira "/_framework/..."
            }
        }

        return routes;
    }

    // Só o arquivo de recursos entra: "./_framework/resource-collection.<impressão>.js" ou ".js.gz", sem subcaminho.
    private static bool IsResourceCollectionFile(string value)
    {
        const string prefix = "./_framework/resource-collection.";
        return value.StartsWith(prefix, StringComparison.Ordinal)
            && (value.EndsWith(".js", StringComparison.Ordinal) || value.EndsWith(".js.gz", StringComparison.Ordinal))
            && !value.AsSpan(prefix.Length).Contains('/');
    }

    // Endpoint anônimo de teste; sem método informado, é um GET.
    private static RouteEndpoint AnonymousEndpoint(string route, params object[] metadata)
    {
        var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse(route), 0);
        builder.Metadata.Add(new AllowAnonymousAttribute());
        if (!metadata.OfType<IHttpMethodMetadata>().Any())
            builder.Metadata.Add(new HttpMethodMetadata(["GET"]));
        foreach (var item in metadata)
            builder.Metadata.Add(item);
        return (RouteEndpoint)builder.Build();
    }

    private static bool HasExplicitPolicy(RouteEndpoint endpoint, IReadOnlySet<string> anonymousRoutes)
    {
        var route = Route(endpoint);
        var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
        return anonymous ? IsAllowedAnonymous(endpoint, route, anonymousRoutes)
            : policies.Any(p => !string.IsNullOrWhiteSpace(p.Policy));
    }

    // Anônimo só com os métodos declarados no endpoint (sem eles, vale qualquer método): na API, cada par (método, rota)
    // da lista; no site e nos arquivos estáticos, só leitura.
    private static bool IsAllowedAnonymous(RouteEndpoint endpoint, string route, IReadOnlySet<string> anonymousRoutes)
    {
        if (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods is not { Count: > 0 } methods)
            return false;
        if (methods.All(method => AnonymousApiRoutes.Contains($"{method} {route}")))
            return true;
        return methods.All(SiteMethods.Contains)
            && (IsAllowedSiteRoute(endpoint, route, anonymousRoutes) || IsStaticAsset(endpoint, route));
    }

    private static bool IsAllowedSiteRoute(RouteEndpoint endpoint, string route, IReadOnlySet<string> anonymousRoutes) =>
        anonymousRoutes.Contains(route) && (!SitePageRoutes.Contains(route) || IsComponentPage(endpoint));

    // O MapRazorComponents põe o tipo do componente em cada endpoint de página que cria.
    private static bool IsComponentPage(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<ComponentTypeMetadata>() is not null;

    // O MapStaticAssets põe o descritor do arquivo em cada endpoint que cria, com a rota que vira o padrão.
    private static bool IsStaticAsset(RouteEndpoint endpoint, string route) =>
        endpoint.Metadata.GetMetadata<StaticAssetDescriptor>() is { } asset && route == "/" + asset.Route.TrimStart('/');

    [Fact]
    public async Task CA_TEN_006_Request_without_session_returns_401()
    {
        await using var api = new ApiFactory(db, "Development");
        using var anonymous = api.CreateClient();

        using var me = await anonymous.GetAsync(ApiRoutes.Me);
        using var members = await anonymous.GetAsync(ApiRoutes.Members(DevSeed.ClubA));
        using var adminClub = await anonymous.GetAsync(ApiRoutes.AdminClub(DevSeed.ClubA));

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, members.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, adminClub.StatusCode);
    }

    [Fact]
    public async Task CA_TEN_006_Fallback_policy_denies_anonymous()
    {
        await using var api = new ApiFactory(db, "Production");

        var fallback = await api.Services.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }
}
