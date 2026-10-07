namespace Dbvprovas.Contracts;

public sealed record MeResponse(string Name, IReadOnlyList<ClubSummary> Clubs);

public sealed record ClubSummary(Guid Id, string Name);

public sealed record ClubResponse(Guid Id, string Name);

public sealed record MemberResponse(Guid MembershipId, string Name);

public sealed record DevAccountResponse(Guid AccountId, string Label);

public sealed record DevSessionRequest(Guid AccountId);

// Subconjunto da resposta do esquema de bearer token do ASP.NET Core.
public sealed record DevSessionResponse(string AccessToken, long ExpiresIn);
