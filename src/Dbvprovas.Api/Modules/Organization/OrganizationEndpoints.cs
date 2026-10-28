using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Organization;

public static class OrganizationEndpoints
{
    public static RouteGroupBuilder MapClubOrganization(this IEndpointRouteBuilder app)
    {
        var club = app.MapGroup("/api/clubs/{clubId:guid}").RequireAuthorization(Policies.Member)
            .AddEndpointFilter<ClubMembershipFilter>().WithTags("Organization");
        club.MapGet("/units", async (UnitService service, CancellationToken ct) => (await service.ListAsync(ct)).ToHttp());
        club.MapPost("/units", async (UnitRequest body, UnitService service, CancellationToken ct) => (await service.CreateAsync(body, ct)).ToHttp());
        club.MapPut("/units/{unitId:guid}", async (Guid unitId, UnitRequest body, UnitService service, CancellationToken ct) =>
            (await service.UpdateAsync(unitId, body, ct)).ToHttp());
        club.MapGet("/leaders", async (RoleAssignmentService service, CancellationToken ct) => (await service.ListLeadersAsync(ct)).ToHttp());
        club.MapPost("/role-assignments", async (AssignRoleRequest body, RoleAssignmentService service, CancellationToken ct) =>
            (await service.AssignAsync(body, ct)).ToHttp());
        club.MapPost("/role-assignments/{assignmentId:guid}/end", async (Guid assignmentId, RoleAssignmentService service, CancellationToken ct) =>
            (await service.EndAsync(assignmentId, ct)).ToHttp());
        return club;
    }
}
