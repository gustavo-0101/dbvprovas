using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Joining;

// Pedido de entrada (RN-JOIN-001 a 008); pertence ao clube de destino.
public sealed class JoinRequest : IClubOwned
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public Guid CandidatePersonId { get; init; }
    public Guid CreatedByAccountId { get; init; }
    public MembershipKind Kind { get; init; } = MembershipKind.Leader;
    public JoinPath Path { get; init; }
    public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Queued;
    public Guid ConsentId { get; init; }
    public DateTimeOffset QueuedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? ReminderSentAt { get; set; }
    public Guid? DecidedByPersonId { get; set; }
    public ReasonCode? ReasonCode { get; set; }
    public EndCause? CloseCause { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}
