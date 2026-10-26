namespace Dbvprovas.Contracts;

public static partial class ApiRoutes
{
    public const string FoundingRequests = "api/founding-requests";
    public const string MyFoundingRequests = "api/me/founding-requests";
    public const string AdminFoundingRequests = "api/admin/founding-requests";

    public static string CancelMyFoundingRequest(Guid requestId) => $"api/me/founding-requests/{requestId}/cancel";

    public static string ApproveFoundingRequest(Guid requestId) => $"api/admin/founding-requests/{requestId}/approve";

    public static string RejectFoundingRequest(Guid requestId) => $"api/admin/founding-requests/{requestId}/reject";

    public static string ClubSettings(Guid clubId) => $"api/clubs/{clubId}/settings";

    public static string ClubInviteCode(Guid clubId) => $"api/clubs/{clubId}/invite-code";
}

public static partial class ProblemCodes
{
    public const string FoundingAlreadyFounder = "founding.already_founder";
    public const string FoundingIsDirector = "founding.is_director";
    public const string FoundingOwnRequest = "founding.own_request";
    public const string FoundingExceptionRequired = "founding.exception_required";
    public const string FoundingExceptionNotApplicable = "founding.exception_not_applicable";
}

// RequestException: quem já fundou pede exceção (D-157); o admin decide com motivo.
public sealed record CreateFoundingRequest(string ClubName, string City, string? Church, bool RequestException, string ConsentTermVersion, bool ConsentAccepted);

public sealed record FoundingRequestResponse(Guid Id, string ClubName, string City, string? Church, FoundingRequestStatus Status, bool ExceptionRequested, DateTimeOffset CreatedAt);

// Mínimo do solicitante para o admin decidir (RN-AUD-006): nome, maioridade e se já fundou.
public sealed record AdminFoundingRequestResponse(Guid Id, string ClubName, string City, string? Church, bool ExceptionRequested,
    string RequesterName, bool RequesterIsAdult, bool RequesterFoundedBefore, DateTimeOffset CreatedAt);

public sealed record ApproveFoundingRequest(bool GrantException, ReasonCode? ExceptionReason);

public sealed record RejectFoundingRequest(ReasonCode? Reason);

public sealed record FoundingApprovedResponse(Guid ClubId);

public sealed record ClubSettingsRequest(string Name, string City, string? Church);

public sealed record ClubSettingsResponse(string Name, string City, string? Church, string InviteCode);
