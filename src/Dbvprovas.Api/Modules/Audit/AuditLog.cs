using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Audit;

// Registro de auditoria (RN-AUD-001, RN-AUD-002, D-160): IDs e códigos. Entra no mesmo SaveChanges da decisão:
// ou os dois ficam, ou nenhum (CA-AUD-010).
public sealed class AuditLog(AppDbContext db, TimeProvider clock)
{
    public AuditEntry Record(AuditAction action, AuditTarget targetType, Guid targetId, Guid? clubId, AuditScope scope,
        Guid? actorPersonId, ReasonCode? reason = null, EndCause? cause = null, Guid? adminAccessRequestId = null,
        IReadOnlyCollection<AdminDataField>? fields = null)
    {
        var entry = new AuditEntry
        {
            Id = Guid.CreateVersion7(),
            ClubId = clubId,
            Scope = scope,
            ActorPersonId = actorPersonId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            ReasonCode = reason,
            Cause = cause,
            AdminAccessRequestId = adminAccessRequestId,
            Fields = fields?.Select(f => f.ToString()).ToArray() ?? [],
            OccurredAt = clock.GetUtcNow(),
        };
        db.AuditEntries.Add(entry);
        return entry;
    }
}
