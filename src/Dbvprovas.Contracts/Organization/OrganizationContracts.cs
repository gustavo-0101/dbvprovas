namespace Dbvprovas.Contracts;

public static partial class ApiRoutes
{
    public const string AdminClubs = "api/admin/clubs";

    public static string Units(Guid clubId) => $"api/clubs/{clubId}/units";

    public static string Unit(Guid clubId, Guid unitId) => $"api/clubs/{clubId}/units/{unitId}";

    public static string Leaders(Guid clubId) => $"api/clubs/{clubId}/leaders";

    public static string RoleAssignments(Guid clubId) => $"api/clubs/{clubId}/role-assignments";

    public static string EndRoleAssignment(Guid clubId, Guid assignmentId) => $"api/clubs/{clubId}/role-assignments/{assignmentId}/end";

    public static string DirectorNominations(Guid clubId) => $"api/clubs/{clubId}/director-nominations";

    public static string CurrentDirectorNomination(Guid clubId) => $"api/clubs/{clubId}/director-nominations/current";

    public static string CancelDirectorNomination(Guid clubId, Guid nominationId) => $"api/clubs/{clubId}/director-nominations/{nominationId}/cancel";

    public static string AcceptDirectorNomination(Guid clubId, Guid nominationId) => $"api/clubs/{clubId}/director-nominations/{nominationId}/accept";

    public static string DeclineDirectorNomination(Guid clubId, Guid nominationId) => $"api/clubs/{clubId}/director-nominations/{nominationId}/decline";

    public static string SuspendMember(Guid clubId, Guid membershipId) => $"api/clubs/{clubId}/members/{membershipId}/suspend";

    public static string RemoveMember(Guid clubId, Guid membershipId) => $"api/clubs/{clubId}/members/{membershipId}/remove";

    public static string ReactivateMember(Guid clubId, Guid membershipId) => $"api/clubs/{clubId}/members/{membershipId}/reactivate";

    public static string AdminClub(Guid clubId) => $"api/admin/clubs/{clubId}";

    public static string AdminDirectorCandidates(Guid clubId) => $"api/admin/clubs/{clubId}/director-candidates";

    public static string AdminAppointDirector(Guid clubId) => $"api/admin/clubs/{clubId}/director";

    public static string AdminReplaceDirector(Guid clubId) => $"api/admin/clubs/{clubId}/director/replace";
}

public static partial class ProblemCodes
{
    public const string UnitInvalidRange = "unit.invalid_range";
    public const string UnitSexConflict = "unit.sex_conflict";
    public const string RoleDirectorForbidden = "role.director_forbidden";
    public const string RoleUnknown = "role.unknown";
    public const string RoleTargetIneligible = "role.target_ineligible";
    public const string RoleUnitRequired = "role.unit_required";
    public const string RoleUnitNotAllowed = "role.unit_not_allowed";
    public const string RoleUnitInactive = "role.unit_inactive";
    public const string RoleUnitSexMismatch = "role.unit_sex_mismatch";
    public const string RoleUniqueTaken = "role.unique_taken";
    public const string RoleAlreadyAssigned = "role.already_assigned";
    public const string NominationTargetIneligible = "nomination.target_ineligible";
    public const string NominationPendingExists = "nomination.pending_exists";
    public const string NominationNominatorNotDirector = "nomination.nominator_not_director";
    public const string DirectorExists = "director.exists";
    public const string DirectorMissing = "director.missing";
    public const string DirectorTargetIneligible = "director.target_ineligible";
    public const string MembershipTargetDirector = "membership.target_director";
    public const string MembershipNotSuspended = "membership.not_suspended";
    public const string MembershipEnded = "membership.ended";
    public const string MembershipOtherActive = "membership.other_active";
}

public sealed record UnitRequest(string Name, Sex Sex, int MinAge, int MaxAge, UnitStatus Status = UnitStatus.Active);

public sealed record UnitResponse(Guid Id, string Name, Sex Sex, int MinAge, int MaxAge, UnitStatus Status);

public sealed record LeaderRoleResponse(Guid AssignmentId, RoleCode Role, Guid? UnitId);

public sealed record LeaderResponse(Guid MembershipId, string Name, MembershipStatus Status, IReadOnlyList<LeaderRoleResponse> Roles);

public sealed record AssignRoleRequest(Guid MembershipId, RoleCode Role, Guid? UnitId);

public sealed record RoleAssignmentResponse(Guid AssignmentId, Guid MembershipId, RoleCode Role, Guid? UnitId);

public sealed record NominateDirectorRequest(Guid NomineeMembershipId);

public sealed record NominationResponse(Guid Id, Guid NominatorMembershipId, Guid NomineeMembershipId, NominationStatus Status, DateTimeOffset ExpiresAt);

public sealed record CurrentNominationResponse(NominationResponse? Nomination);

public sealed record MemberStatusRequest(ReasonCode? Reason);

public sealed record AdminClubResponse(Guid Id, string Name, string City, bool HasDirector);

// Mínimo da tarefa do admin ao nomear (RN-AUD-006): nome e maioridade.
public sealed record DirectorCandidateResponse(Guid MembershipId, string Name, bool IsAdult);

public sealed record AppointDirectorRequest(Guid MembershipId);

public sealed record ReplaceDirectorRequest(Guid MembershipId, ReasonCode? Reason);
