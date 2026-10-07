using Dbvprovas.Api.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Dbvprovas.Api.Modules.Audit;

// RF-AUD-001
public static class HealthEndpoints
{
    public static IServiceCollection AddHealth(this IServiceCollection services)
    {
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>(name: "database", tags: ["ready"]);
        return services;
    }

    public static void MapHealth(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();
    }
}
