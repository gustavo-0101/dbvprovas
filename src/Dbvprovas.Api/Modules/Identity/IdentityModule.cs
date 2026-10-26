namespace Dbvprovas.Api.Modules.Identity;

// Registro do módulo ID: login, sessão e cadastro. O login de dev do R0 só existe em Development (D-159).
public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services) => services;

    public static WebApplication MapIdentityModule(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
            app.MapDevLogin();
        return app;
    }
}
