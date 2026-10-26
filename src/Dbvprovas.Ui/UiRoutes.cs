namespace Dbvprovas.Ui;

// Rotas das telas do R1, iguais no app e no site (D-104), relativas à base do app. Cada página declara a sua no @page.
public static class UiRoutes
{
    public const string Login = "";
    public const string Profile = "profile";
    public const string Inbox = "inbox";
    public const string MyClub = "club";
    public const string NotFound = "not-found";
    public const string Founding = "founding";
    public const string Join = "join";
    public const string MyMemberships = "me/memberships";
    public const string ClubSettings = "club/settings";
    public const string ClubUnits = "club/units";
    public const string ClubLeaders = "club/leaders";
    public const string ClubDirector = "club/director";
    public const string ClubJoinRequests = "club/join-requests";
    public const string ClubMembers = "club/members";
    public const string ClubAudit = "club/audit";
    public const string ClubAdminAccess = "club/admin-access";
    public const string AdminFoundingRequests = "admin/founding-requests";
    public const string AdminClubs = "admin/clubs";
    public const string AdminAudit = "admin/audit";
    public const string AdminAccessRequests = "admin/access-requests";

    public static string ClubMember(Guid membershipId) => $"club/members/{membershipId}";

    public static string AdminClubDirector(Guid clubId) => $"admin/clubs/{clubId}/director";

    public static string AdminAccessRequest(Guid requestId) => $"admin/access-requests/{requestId}";
}
