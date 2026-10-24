using Dbvprovas.Api.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Dbvprovas.Api.Modules.Audit;

// RF-AUD-001
public static class HealthEndpoints
{
    public static IServiceCollection AddHealth(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(name: "database", tags: ["ready"])
            .AddCheck<RowSecurityCheck>(RowSecurityCheck.Name, tags: ["ready"]);
        return services;
    }

    // Anônimas e só GET: rota anônima tem o método declarado (CA-TEN-006).
    public static void MapHealth(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .WithMetadata(new HttpMethodMetadata(["GET"])).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") })
            .WithMetadata(new HttpMethodMetadata(["GET"])).AllowAnonymous();
    }
}

// RNF-TEN-001, D-121: pronta só se a conexão do app estiver sujeita ao RLS. Com o papel dono, o banco ignoraria as
// políticas e só o filtro do EF separaria os clubes, sem nenhum teste funcional perceber.
internal sealed class RowSecurityCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public const string Name = "row-security";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("SELECT row_security_active('memberships')");
        return await command.ExecuteScalarAsync(cancellationToken) is true ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
    }
}
