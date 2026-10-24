namespace Dbvprovas.Api.Modules.Tenancy;

public enum ClubStatus
{
    Active,
}

// O "inquilino" (RN-TEN-001). Cidade e igreja servem à busca (RN-JOIN-001); o convite, à entrada por código (D-162).
public sealed class Club
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public required string City { get; set; }
    public string? Church { get; set; }
    public required string InviteCode { get; set; }
    public ClubStatus Status { get; init; } = ClubStatus.Active;
    public Guid? FoundingRequestId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
