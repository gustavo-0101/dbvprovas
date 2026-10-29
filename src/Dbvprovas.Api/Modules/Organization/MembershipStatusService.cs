using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Audit;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Organization;

// Suspender, remover e reativar (RF-JOIN-005, RF-JOIN-007, PERM-JOIN-004, PERM-JOIN-007, RN-JOIN-011, RN-JOIN-015, D-151, D-155).
// Nenhum aviso: RN-NTF-002 não lista estes eventos.
public sealed class MembershipStatusService(
    AppDbContext db, ClubContext context, ActorService actors, RoleTermination roles, AuditLog audit, TimeProvider clock)
{
    public Task<Outcome> SuspendAsync(Guid membershipId, MemberStatusRequest body, CancellationToken ct) =>
        ChangeAsync(membershipId, body.Reason, ReasonOperation.Suspend, ct);

    public Task<Outcome> RemoveAsync(Guid membershipId, MemberStatusRequest body, CancellationToken ct) =>
        ChangeAsync(membershipId, body.Reason, ReasonOperation.Remove, ct);

    public Task<Outcome> ReactivateAsync(Guid membershipId, MemberStatusRequest body, CancellationToken ct) =>
        ChangeAsync(membershipId, body.Reason, ReasonOperation.Reactivate, ct);

    private async Task<Outcome> ChangeAsync(Guid membershipId, ReasonCode? reason, ReasonOperation operation, CancellationToken ct)
    {
        var actor = await actors.GetClubActorAsync(ct);
        if (actor is null)
            return Outcome.NotFound;
        if (!actor.IsDirectorOrSecretary)
            return Outcome.Forbidden();
        if (membershipId == actor.MembershipId)
            return Outcome.Forbidden(ProblemCodes.SelfAction);
        if (!ReasonCodes.IsAllowed(operation, reason))
            return Outcome.Invalid(ProblemCodes.ReasonRequired);

        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var target = await db.Memberships.AsNoTracking().Where(m => m.Id == membershipId && m.ClubId == context.ClubId)
            .Select(m => new { m.ClubId, m.PersonId, m.Status }).SingleOrDefaultAsync(ct);
        if (target is null)
            return Outcome.NotFound;

        if (operation == ReasonOperation.Reactivate)
        {
            // Ordem das travas: consentimento, depois participação (RowLocks).
            var consentId = await db.Consents.Where(c => c.PersonId == target.PersonId && c.ClubId == target.ClubId
                && c.RevokedAt == null && c.EndedAt == null).Select(c => (Guid?)c.Id).SingleOrDefaultAsync(ct);
            if (consentId is not { } consent)
                return Outcome.Forbidden(ProblemCodes.ConsentRequired);
            await RowLocks.LockConsentAsync(db, consent, exclusive: false, ct);
            // A leitura que vale é a de depois da trava: a revogação pode ter terminado enquanto este pedido esperava.
            if (!await db.Consents.AnyAsync(c => c.Id == consent && c.RevokedAt == null && c.EndedAt == null, ct))
                return Outcome.Forbidden(ProblemCodes.ConsentRequired);
        }

        await RowLocks.LockMembershipsAsync(db, [membershipId], ct);
        var isDirector = await db.RoleAssignments.AnyAsync(r => r.MembershipId == membershipId && r.Role == RoleCode.Director && r.EndedAt == null, ct);
        if (isDirector)
            return Outcome.Forbidden(ProblemCodes.MembershipTargetDirector);

        try
        {
            var changed = operation switch
            {
                ReasonOperation.Suspend => await db.Memberships.Where(m => m.Id == membershipId && m.Status == MembershipStatus.Active)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, MembershipStatus.Suspended).SetProperty(m => m.SuspendedAt, now)
                        .SetProperty(m => m.StatusReason, reason).SetProperty(m => m.StatusCause, (EndCause?)null), ct),
                ReasonOperation.Remove => await db.Memberships.Where(m => m.Id == membershipId && m.Status != MembershipStatus.Ended)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, MembershipStatus.Ended).SetProperty(m => m.EndedAt, now)
                        .SetProperty(m => m.SuspendedAt, (DateTimeOffset?)null).SetProperty(m => m.StatusReason, reason)
                        .SetProperty(m => m.StatusCause, EndCause.RemovedByBoard), ct),
                _ => await db.Memberships.Where(m => m.Id == membershipId && m.Status == MembershipStatus.Suspended)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, MembershipStatus.Active).SetProperty(m => m.SuspendedAt, (DateTimeOffset?)null)
                        .SetProperty(m => m.StatusReason, reason).SetProperty(m => m.StatusCause, (EndCause?)null), ct),
            };
            if (changed == 0)
                return target.Status switch
                {
                    MembershipStatus.Ended => Outcome.Conflict(ProblemCodes.MembershipEnded),
                    _ when operation == ReasonOperation.Reactivate => Outcome.Conflict(ProblemCodes.MembershipNotSuspended),
                    _ => Outcome.Conflict(),
                };
        }
        catch (Exception e) when (DbErrors.IsUniqueViolation(e, DbConstraints.OneActiveMembershipPerPerson))
        {
            return Outcome.Conflict(ProblemCodes.MembershipOtherActive);
        }

        if (operation != ReasonOperation.Reactivate)
            await roles.EndAllAsync(membershipId, operation == ReasonOperation.Suspend ? EndCause.MembershipSuspended : EndCause.MembershipEnded, actor.PersonId, ct);
        var action = operation switch
        {
            ReasonOperation.Suspend => AuditAction.MembershipSuspended,
            ReasonOperation.Remove => AuditAction.MembershipRemoved,
            _ => AuditAction.MembershipReactivated,
        };
        audit.Record(action, AuditTarget.Membership, membershipId, target.ClubId, AuditScope.Club, actor.PersonId, reason: reason);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Outcome.Done;
    }
}
