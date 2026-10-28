using Dbvprovas.Api.Modules.Tenancy.Settings;

namespace Dbvprovas.Api.Modules.Tenancy.Founding;

// Registro do módulo de fundação: fundação, dados do clube e convite.
public static class FoundingModule
{
    public static IServiceCollection AddFoundingModule(this IServiceCollection services)
    {
        services.AddScoped<FoundingRequestService>();
        services.AddScoped<FoundingDecisionService>();
        services.AddScoped<ClubSettingsService>();
        return services;
    }

    public static WebApplication MapFoundingModule(this WebApplication app)
    {
        app.MapFoundingRequests();
        app.MapFoundingDecisions();
        app.MapClubSettings();
        return app;
    }
}
