namespace Dbvprovas.Contracts;

// Rotas relativas à origem da API (RNF-PRV-002).
public static class ApiRoutes
{
    public const string Me = "api/me";
    public const string DevAccounts = "api/dev/accounts";
    public const string DevSessions = "api/dev/sessions";

    public static string Club(Guid clubId) => $"api/clubs/{clubId}";

    public static string Members(Guid clubId) => $"api/clubs/{clubId}/members";

    public static string Member(Guid clubId, Guid membershipId) => $"api/clubs/{clubId}/members/{membershipId}";
}
