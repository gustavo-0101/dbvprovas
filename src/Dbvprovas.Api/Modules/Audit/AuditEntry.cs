using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Audit;

// Registro de auditoria (RN-AUD-001, RN-AUD-002): quem, o quê, quando e sobre o quê, só com IDs e códigos.
public sealed class AuditEntry : IMaybeClubOwned
{
    public Guid Id { get; init; }
    public Guid? ClubId { get; init; }
    public AuditScope Scope { get; init; }
    public Guid? ActorPersonId { get; init; }
    public AuditAction Action { get; init; }
    public AuditTarget TargetType { get; init; }
    public Guid TargetId { get; init; }
    public ReasonCode? ReasonCode { get; init; }
    public EndCause? Cause { get; init; }
    public Guid? AdminAccessRequestId { get; init; }
    public string[] Fields { get; init; } = [];
    public DateTimeOffset OccurredAt { get; init; }
}
