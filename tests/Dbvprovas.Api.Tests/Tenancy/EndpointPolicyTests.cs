using System.Net;
using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.Extensions.DependencyInjection;

namespace Dbvprovas.Api.Tests.Tenancy;

public sealed class EndpointPolicyTests(PostgresFixture db)
{
    private static readonly string[] AnonymousApiRoutes =
    [
        "/api/dev/accounts",
        "/api/dev/sessions",
        "/health/live",
        "/health/ready",
        "/openapi/{documentName}.json",
    ];

    // Páginas do site e o endpoint de redirecionamento do Blazor (D-112). Os arquivos estáticos entram
    // pelo marcador do MapStaticAssets e a lista de recursos pelo mapa de importação das páginas;
    // nunca por prefixo de caminho.
    private static readonly string[] AnonymousSiteRoutes =
    [
        "/",
        "/club",
        "/not-found",
        "/_framework/opaque-redirect",
    ];

    private static readonly string[] ResourceCollectionNames =
    [
        "_framework/resource-collection.js",
        "_framework/resource-collection.js.gz",
    ];

    [Fact]
    public async Task CA_TEN_006_Every_endpoint_has_explicit_policy()
    {
        await using var api = new ApiFactory(db, "Development");
        var endpoints = RouteEndpoints(api);

        Assert.NotEmpty(endpoints);
        var allowed = AnonymousRoutes(endpoints);
        foreach (var endpoint in endpoints)
            Assert.True(HasExplicitPolicy(endpoint, allowed), $"Endpoint without explicit policy: {endpoint.RoutePattern.RawText}");
    }

    [Fact]
    public void CA_TEN_006_Anonymous_endpoint_outside_allowlist_is_rejected()
    {
        var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/export"), 0);
        builder.Metadata.Add(new AllowAnonymousAttribute());
        Assert.False(HasExplicitPolicy((RouteEndpoint)builder.Build(), FixedRoutes()));
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
    [Fact]
    public async Task CA_TEN_006_Resource_collection_routes_come_from_the_page_import_map()
    {
        await using var api = new ApiFactory(db, "Development");
        var endpoints = RouteEndpoints(api);
        var allowed = AnonymousRoutes(endpoints);

        var declared = allowed.Where(route => route.StartsWith("/_framework/resource-collection", StringComparison.Ordinal)).ToList();

        Assert.Equal(4, declared.Count);
        Assert.False(HasExplicitPolicy(AnonymousEndpoint("/_framework/resource-collection.zzzzz.js"), allowed));
    }

    private static List<RouteEndpoint> RouteEndpoints(ApiFactory api) =>
        api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

    private static string Route(RouteEndpoint endpoint) => "/" + endpoint.RoutePattern.RawText!.TrimStart('/');

    private static HashSet<string> FixedRoutes() => [.. AnonymousApiRoutes, .. AnonymousSiteRoutes];

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
                if (!imports.TryGetValue(name, out var fingerprinted))
                    continue;
                routes.Add("/" + name);
                routes.Add(fingerprinted[1..]); // "./_framework/..." vira "/_framework/..."
            }
        }

        return routes;
    }

    private static RouteEndpoint AnonymousEndpoint(string route, params object[] metadata)
    {
        var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse(route), 0);
        builder.Metadata.Add(new AllowAnonymousAttribute());
        foreach (var item in metadata)
            builder.Metadata.Add(item);
        return (RouteEndpoint)builder.Build();
    }

    private static bool HasExplicitPolicy(RouteEndpoint endpoint, IReadOnlySet<string> anonymousRoutes)
    {
        var route = Route(endpoint);
        var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
        return anonymous ? anonymousRoutes.Contains(route) || IsStaticAsset(endpoint, route)
            : policies.Any(p => !string.IsNullOrWhiteSpace(p.Policy));
    }

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

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, members.StatusCode);
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
