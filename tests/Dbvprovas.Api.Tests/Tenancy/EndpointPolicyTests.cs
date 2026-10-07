using System.Net;
using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Routing;
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

    [Fact]
    public async Task CA_TEN_006_Every_endpoint_has_explicit_policy()
    {
        await using var api = new ApiFactory(db, "Development");
        var endpoints = api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        Assert.NotEmpty(endpoints);
        foreach (var endpoint in endpoints)
        {
            var pattern = "/" + endpoint.RoutePattern.RawText!.TrimStart('/');
            var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            var isApi = pattern.StartsWith("/api/") || pattern.StartsWith("/health/") || pattern.StartsWith("/openapi/");

            if (isApi && anonymous)
                Assert.Contains(pattern, AnonymousApiRoutes);
            else
                Assert.True(anonymous || policies.Any(p => !string.IsNullOrWhiteSpace(p.Policy)), $"Rota sem política explícita: {pattern}");
        }
    }

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
