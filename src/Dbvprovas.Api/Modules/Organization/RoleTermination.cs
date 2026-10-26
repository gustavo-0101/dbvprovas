using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Audit;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Organization;

// Encerra cargos vigentes de uma participação, com auditoria (RN-ORG-006, D-155, D-158). Se o cargo encerrado for o de
// diretor, cancela a indicação pendente feita por ele (D-163). Roda no clube da requisição, na transação de quem chama,
// depois de travar a participação (RowLocks); o SaveChanges é de quem chama.
public sealed class RoleTermination(AppDbContext db, AuditLog audit, TimeProvider clock)
{
    public async Task<int> EndAsync(Guid membershipId, Func<RoleCode, bool> which, EndCause cause, Guid? actorPersonId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var active = await db.RoleAssignments.Where(r => r.MembershipId == membershipId && r.EndedAt == null).ToListAsync(ct);
        var ending = active.Where(r => which(r.Role)).ToList();
        foreach (var role in ending)
        {
            role.EndedAt = now;
            role.EndedByPersonId = actorPersonId;
            role.EndCause = cause;
            audit.Record(AuditAction.RoleEnded, AuditTarget.RoleAssignment, role.Id, role.ClubId, AuditScope.Club, actorPersonId, cause: cause);
        }

        if (ending.Any(r => r.Role == RoleCode.Director))
        {
            var pending = await db.DirectorNominations
                .Where(n => n.NominatorMembershipId == membershipId && n.Status == NominationStatus.Pending)
                .ToListAsync(ct);
            foreach (var nomination in pending)
            {
                nomination.Status = NominationStatus.Cancelled;
                nomination.ClosedAt = now;
                audit.Record(AuditAction.DirectorNominationCancelled, AuditTarget.DirectorNomination, nomination.Id,
                    nomination.ClubId, AuditScope.Club, actorPersonId, cause: cause);
            }
        }

        return ending.Count;
    }

    public Task<int> EndAllAsync(Guid membershipId, EndCause cause, Guid? actorPersonId, CancellationToken ct) =>
        EndAsync(membershipId, _ => true, cause, actorPersonId, ct);
}
