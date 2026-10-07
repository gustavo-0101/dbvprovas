namespace Dbvprovas.Api.Modules.Tenancy;

public enum ClubStatus
{
    Active,
}

public sealed class Club
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public ClubStatus Status { get; init; } = ClubStatus.Active;
}
