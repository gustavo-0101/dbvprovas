namespace Dbvprovas.Api.Modules.Organization;

// Registro do módulo ORG: unidades, cargos, direção e suspensão.
public static class OrganizationModule
{
    public static IServiceCollection AddOrganizationModule(this IServiceCollection services)
    {
        services.AddScoped<UnitService>();
        services.AddScoped<RoleAssignmentService>();
        services.AddScoped<DirectorNominationService>();
        services.AddScoped<AdminDirectorService>();
        services.AddScoped<MembershipStatusService>();
        return services;
    }

    public static WebApplication MapOrganizationModule(this WebApplication app)
    {
        app.MapClubOrganization();
        app.MapAdminDirector();
        return app;
    }
}
