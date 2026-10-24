namespace Dbvprovas.Contracts;

// "Quem sou eu" (RF-TEN-001, RF-ID-005): só as participações ativas, com os cargos vigentes, e o papel de admin desta requisição.
public sealed record MeResponse(string? Name, IReadOnlyList<ClubSummary> Clubs, bool ProfileComplete, bool IsAdult, bool IsPlatformAdmin);

public sealed record ClubSummary(Guid Id, string Name, Guid MembershipId, IReadOnlyList<RoleCode> Roles);

public sealed record ClubResponse(Guid Id, string Name);

// Nome e unidade (PERM-PRV-002); a unidade é a do conselheiro, quando houver.
public sealed record MemberResponse(Guid MembershipId, string Name, string? UnitName);

public sealed record DevAccountResponse(Guid AccountId, string Label);

public sealed record DevSessionRequest(Guid AccountId);

// Subconjunto da resposta do esquema de bearer token do ASP.NET Core.
public sealed record DevSessionResponse(string AccessToken, long ExpiresIn);
