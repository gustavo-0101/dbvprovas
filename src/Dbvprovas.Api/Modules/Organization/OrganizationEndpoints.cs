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
        club.MapPost("/director-nominations", async (NominateDirectorRequest body, DirectorNominationService service, CancellationToken ct) =>
            (await service.NominateAsync(body, ct)).ToHttp());
        club.MapGet("/director-nominations/current", async (DirectorNominationService service, CancellationToken ct) =>
            (await service.CurrentAsync(ct)).ToHttp());
        club.MapPost("/director-nominations/{nominationId:guid}/cancel", async (Guid nominationId, DirectorNominationService service, CancellationToken ct) =>
            (await service.CancelAsync(nominationId, ct)).ToHttp());
        club.MapPost("/director-nominations/{nominationId:guid}/accept", async (Guid nominationId, DirectorNominationService service, CancellationToken ct) =>
            (await service.AcceptAsync(nominationId, ct)).ToHttp());
        club.MapPost("/director-nominations/{nominationId:guid}/decline", async (Guid nominationId, DirectorNominationService service, CancellationToken ct) =>
            (await service.DeclineAsync(nominationId, ct)).ToHttp());
        return club;
    }

    public static void MapAdminDirector(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/clubs").RequireAuthorization(Policies.PlatformAdmin).WithTags("Organization");
        admin.MapGet("", async (string? query, AdminDirectorService service, CancellationToken ct) => (await service.ListClubsAsync(query, ct)).ToHttp());
        admin.MapGet("/{clubId:guid}/director-candidates", async (Guid clubId, AdminDirectorService service, CancellationToken ct) =>
            (await service.CandidatesAsync(clubId, ct)).ToHttp());
        admin.MapPost("/{clubId:guid}/director", async (Guid clubId, AppointDirectorRequest body, AdminDirectorService service, CancellationToken ct) =>
            (await service.AppointAsync(clubId, body, ct)).ToHttp());
        admin.MapPost("/{clubId:guid}/director/replace", async (Guid clubId, ReplaceDirectorRequest body, AdminDirectorService service, CancellationToken ct) =>
            (await service.ReplaceAsync(clubId, body, ct)).ToHttp());
    }
}
