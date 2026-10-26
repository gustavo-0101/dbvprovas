namespace Dbvprovas.Api.Modules.Tenancy.Founding;

// Registro do módulo de fundação: fundação, dados do clube e convite.
public static class FoundingModule
{
    public static IServiceCollection AddFoundingModule(this IServiceCollection services)
    {
        services.AddScoped<FoundingRequestService>();
        return services;
    }

    public static WebApplication MapFoundingModule(this WebApplication app)
    {
        app.MapFoundingRequests();
        return app;
    }
}
