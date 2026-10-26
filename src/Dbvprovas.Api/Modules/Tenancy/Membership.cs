using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Tenancy;

// Todo dado que pertence a um clube implementa esta interface (RN-TEN-001).
public interface IClubOwned
{
    Guid ClubId { get; }
}

// Dado que pertence a um clube quando tem um: consentimento antes da fundação e auditoria da plataforma.
public interface IMaybeClubOwned
{
    Guid? ClubId { get; }
}

public sealed class Membership : IClubOwned
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public Guid PersonId { get; init; }
    public MembershipKind Kind { get; init; } = MembershipKind.Leader;
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? SuspendedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    // Motivo da última suspensão, remoção ou reativação (D-160) e causa automática (EndCause).
    public ReasonCode? StatusReason { get; set; }
    public EndCause? StatusCause { get; set; }
}
