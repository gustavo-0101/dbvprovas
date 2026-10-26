using Dbvprovas.Api.Modules.Audit;
using Dbvprovas.Api.Modules.Notifications;
using Dbvprovas.Api.Modules.Organization;
using Dbvprovas.Api.Modules.Tenancy;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dbvprovas.Api.Infrastructure;

// Serviços comuns do R1, usados por mais de um módulo.
public static class CoreServices
{
    public static IServiceCollection AddR1Core(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System); // PC-12
        services.AddScoped<ClubScope>();
        services.AddOptions<JobOptions>().BindConfiguration(JobOptions.Section);
        services.AddScoped<ActorService>();
        services.AddScoped<AuditLog>();
        services.AddScoped<RoleTermination>();
        services.AddScoped<Notifier>();
        return services;
    }
}
