namespace Dbvprovas.Api.Modules.Organization;

// Registro do módulo ORG: unidades, cargos, direção e suspensão.
public static class OrganizationModule
{
    public static IServiceCollection AddOrganizationModule(this IServiceCollection services) => services;

    public static WebApplication MapOrganizationModule(this WebApplication app) => app;
}
