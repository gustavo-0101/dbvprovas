namespace Dbvprovas.Api.Modules.Audit.AdminAccess;

// Registro do módulo AUD: auditoria e acesso do admin.
public static class AdminAccessModule
{
    public static IServiceCollection AddAdminAccessModule(this IServiceCollection services) => services;

    public static WebApplication MapAdminAccessModule(this WebApplication app) => app;
}
