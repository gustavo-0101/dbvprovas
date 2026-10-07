namespace Dbvprovas.Api.Modules.Tenancy;

// Todo dado que pertence a um clube implementa esta interface (RN-TEN-001).
public interface IClubOwned
{
    Guid ClubId { get; }
}

public sealed class Membership : IClubOwned
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public Guid PersonId { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; set; }
}
