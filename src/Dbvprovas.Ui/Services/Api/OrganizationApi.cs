using Dbvprovas.Contracts;

namespace Dbvprovas.Ui.Services.Api;

// Chamadas de unidades, cargos, direção e suspensão.
public sealed class OrganizationApi(ApiClient api)
{
    internal ApiClient Api { get; } = api;

    public Task<ApiResult<List<UnitResponse>>> ListUnitsAsync(Guid clubId) => Api.SendAsync<List<UnitResponse>>(HttpMethod.Get, ApiRoutes.Units(clubId));

    public Task<ApiResult<UnitResponse>> CreateUnitAsync(Guid clubId, UnitRequest body) => Api.SendAsync<UnitResponse>(HttpMethod.Post, ApiRoutes.Units(clubId), body);

    public Task<ApiResult<UnitResponse>> UpdateUnitAsync(Guid clubId, Guid unitId, UnitRequest body) =>
        Api.SendAsync<UnitResponse>(HttpMethod.Put, ApiRoutes.Unit(clubId, unitId), body);

    public Task<ApiResult<List<LeaderResponse>>> ListLeadersAsync(Guid clubId) => Api.SendAsync<List<LeaderResponse>>(HttpMethod.Get, ApiRoutes.Leaders(clubId));

    public Task<ApiResult<RoleAssignmentResponse>> AssignRoleAsync(Guid clubId, AssignRoleRequest body) =>
        Api.SendAsync<RoleAssignmentResponse>(HttpMethod.Post, ApiRoutes.RoleAssignments(clubId), body);

    public Task<ApiResult> EndRoleAsync(Guid clubId, Guid assignmentId) => Api.SendAsync(HttpMethod.Post, ApiRoutes.EndRoleAssignment(clubId, assignmentId));

    public Task<ApiResult> SuspendAsync(Guid clubId, Guid membershipId, ReasonCode reason) =>
        Api.SendAsync(HttpMethod.Post, ApiRoutes.SuspendMember(clubId, membershipId), new MemberStatusRequest(reason));

    public Task<ApiResult> RemoveAsync(Guid clubId, Guid membershipId, ReasonCode reason) =>
        Api.SendAsync(HttpMethod.Post, ApiRoutes.RemoveMember(clubId, membershipId), new MemberStatusRequest(reason));

    public Task<ApiResult> ReactivateAsync(Guid clubId, Guid membershipId, ReasonCode reason) =>
        Api.SendAsync(HttpMethod.Post, ApiRoutes.ReactivateMember(clubId, membershipId), new MemberStatusRequest(reason));

    public Task<ApiResult<NominationResponse>> NominateAsync(Guid clubId, Guid nomineeMembershipId) =>
        Api.SendAsync<NominationResponse>(HttpMethod.Post, ApiRoutes.DirectorNominations(clubId), new NominateDirectorRequest(nomineeMembershipId));

    public Task<ApiResult<CurrentNominationResponse>> CurrentNominationAsync(Guid clubId) =>
        Api.SendAsync<CurrentNominationResponse>(HttpMethod.Get, ApiRoutes.CurrentDirectorNomination(clubId));

    public Task<ApiResult> CancelNominationAsync(Guid clubId, Guid nominationId) => Api.SendAsync(HttpMethod.Post, ApiRoutes.CancelDirectorNomination(clubId, nominationId));

    public Task<ApiResult> AcceptNominationAsync(Guid clubId, Guid nominationId) => Api.SendAsync(HttpMethod.Post, ApiRoutes.AcceptDirectorNomination(clubId, nominationId));

    public Task<ApiResult> DeclineNominationAsync(Guid clubId, Guid nominationId) => Api.SendAsync(HttpMethod.Post, ApiRoutes.DeclineDirectorNomination(clubId, nominationId));

    public Task<ApiResult<List<AdminClubResponse>>> ListAdminClubsAsync(string? query) =>
        Api.SendAsync<List<AdminClubResponse>>(HttpMethod.Get, string.IsNullOrWhiteSpace(query) ? ApiRoutes.AdminClubs : $"{ApiRoutes.AdminClubs}?query={Uri.EscapeDataString(query)}");

    public Task<ApiResult<AdminClubResponse>> GetAdminClubAsync(Guid clubId) =>
        Api.SendAsync<AdminClubResponse>(HttpMethod.Get, ApiRoutes.AdminClub(clubId));

    public Task<ApiResult<List<DirectorCandidateResponse>>> DirectorCandidatesAsync(Guid clubId) =>
        Api.SendAsync<List<DirectorCandidateResponse>>(HttpMethod.Get, ApiRoutes.AdminDirectorCandidates(clubId));

    public Task<ApiResult> AppointDirectorAsync(Guid clubId, Guid membershipId) =>
        Api.SendAsync(HttpMethod.Post, ApiRoutes.AdminAppointDirector(clubId), new AppointDirectorRequest(membershipId));

    public Task<ApiResult> ReplaceDirectorAsync(Guid clubId, Guid membershipId, ReasonCode reason) =>
        Api.SendAsync(HttpMethod.Post, ApiRoutes.AdminReplaceDirector(clubId), new ReplaceDirectorRequest(membershipId, reason));
}
