using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Audit;
using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Notifications;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Organization;

// Transferência de direção (RF-ORG-003/004, PERM-ORG-003/004, RN-ORG-007, D-014, D-132, D-158, D-163).
public sealed class DirectorNominationService(
    AppDbContext db, ClubContext context, ActorService actors, RoleTermination roles, AuditLog audit, Notifier notifier, TimeProvider clock)
{
    private static readonly TimeSpan Validity = TimeSpan.FromDays(7);

    public async Task<Outcome<NominationResponse>> NominateAsync(NominateDirectorRequest body, CancellationToken ct)
    {
        var actor = await actors.GetClubActorAsync(ct);
        if (actor is null)
            return Outcome.NotFound;
        if (!actor.IsDirector)
            return Outcome.Forbidden();
        if (body.NomineeMembershipId == actor.MembershipId)
            return Outcome.Forbidden(ProblemCodes.SelfAction);

        var now = clock.GetUtcNow();
        var clubId = context.ClubId!.Value;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RowLocks.LockMembershipsAsync(db, [actor.MembershipId, body.NomineeMembershipId], ct);
        if (!await db.RoleAssignments.AnyAsync(r => r.MembershipId == actor.MembershipId && r.ClubId == clubId
                && r.Role == RoleCode.Director && r.EndedAt == null, ct))
            return Outcome.Forbidden();
        var nominee = await (
            from m in db.Memberships
            where m.Id == body.NomineeMembershipId && m.ClubId == clubId
            join p in db.Persons on m.PersonId equals p.Id
            select new { m.Status, m.PersonId, p.BirthDate }).SingleOrDefaultAsync(ct);
        if (nominee is null)
            return Outcome.NotFound;
        if (nominee.Status != MembershipStatus.Active)
            return Outcome.Forbidden(ProblemCodes.NominationTargetIneligible);
        if (nominee.BirthDate is not { } birth || AgeCalculator.AgeOn(birth, BrasiliaCalendar.Today(clock)) < 18)
            return Outcome.Forbidden(ProblemCodes.AgeBelowMinimum);

        // D-163: a pendência vencida não segura a vaga de "uma pendente por clube".
        await db.DirectorNominations.Where(n => n.Status == NominationStatus.Pending && n.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, NominationStatus.Expired).SetProperty(n => n.ClosedAt, n => n.ExpiresAt), ct);
        var nomination = new DirectorNomination
        {
            Id = Guid.CreateVersion7(), ClubId = clubId, NominatorMembershipId = actor.MembershipId, NomineeMembershipId = body.NomineeMembershipId,
            CreatedAt = now, ExpiresAt = now + Validity,
        };
        db.DirectorNominations.Add(nomination);
        audit.Record(AuditAction.DirectorNominated, AuditTarget.DirectorNomination, nomination.Id, clubId, AuditScope.Club, actor.PersonId);
        notifier.Notify(NotificationEvent.DirectorNominated, nomination.Id, clubId, [nominee.PersonId]);
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException e) when (DbErrors.IsUniqueViolation(e, DbConstraints.OnePendingNomination))
        {
            return Outcome.Conflict(ProblemCodes.NominationPendingExists);
        }

        return Outcome<NominationResponse>.Ok(ToResponse(nomination));
    }

    // Diretora e indicado veem a pendência; os demais membros recebem vazio (CA-ORG-004, CA-ORG-005).
    public async Task<Outcome<CurrentNominationResponse>> CurrentAsync(CancellationToken ct)
    {
        var actor = await actors.GetClubActorAsync(ct);
        if (actor is null)
            return Outcome.NotFound;
        var now = clock.GetUtcNow();
        var nomination = await db.DirectorNominations
            .Where(n => n.Status == NominationStatus.Pending && n.ExpiresAt > now).SingleOrDefaultAsync(ct);
        var visible = nomination is not null && (actor.IsDirector || nomination.NomineeMembershipId == actor.MembershipId);
        return Outcome<CurrentNominationResponse>.Ok(new CurrentNominationResponse(visible ? ToResponse(nomination!) : null));
    }

    public async Task<Outcome> CancelAsync(Guid nominationId, CancellationToken ct)
    {
        var actor = await actors.GetClubActorAsync(ct);
        if (actor is null)
            return Outcome.NotFound;
        if (!actor.IsDirector)
            return Outcome.Forbidden();
        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await db.DirectorNominations.AnyAsync(n => n.Id == nominationId && n.ClubId == context.ClubId && n.NominatorMembershipId == actor.MembershipId, ct))
            return Outcome.NotFound;
        var won = await db.DirectorNominations
            .Where(n => n.Id == nominationId && n.Status == NominationStatus.Pending && n.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, NominationStatus.Cancelled).SetProperty(n => n.ClosedAt, now), ct);
        if (won == 0)
            return Outcome.Conflict();
        audit.Record(AuditAction.DirectorNominationCancelled, AuditTarget.DirectorNomination, nominationId, context.ClubId, AuditScope.Club, actor.PersonId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Outcome.Done;
    }

    public async Task<Outcome> AcceptAsync(Guid nominationId, CancellationToken ct)
    {
        var actor = await actors.GetClubActorAsync(ct);
        if (actor is null)
            return Outcome.NotFound;
        var clubId = context.ClubId!.Value;
        var nomination = await db.DirectorNominations.AsNoTracking().SingleOrDefaultAsync(n => n.Id == nominationId && n.ClubId == context.ClubId, ct);
        if (nomination is null)
            return Outcome.NotFound;
        if (nomination.NomineeMembershipId != actor.MembershipId)
            return Outcome.Forbidden();
        if (!actor.IsAdult)
            return Outcome.Forbidden(ProblemCodes.AgeBelowMinimum);

        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RowLocks.LockMembershipsAsync(db, [nomination.NominatorMembershipId, nomination.NomineeMembershipId], ct);
        var nominatorIsDirector = await db.RoleAssignments.AnyAsync(r => r.MembershipId == nomination.NominatorMembershipId
            && r.Role == RoleCode.Director && r.EndedAt == null, ct);
        var nomineeActive = await db.Memberships.AnyAsync(m => m.Id == nomination.NomineeMembershipId && m.Status == MembershipStatus.Active, ct);
        var won = nominatorIsDirector && nomineeActive
            ? await db.DirectorNominations
                .Where(n => n.Id == nominationId && n.Status == NominationStatus.Pending && n.ExpiresAt > now)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, NominationStatus.Accepted).SetProperty(n => n.ClosedAt, now), ct)
            : 0;
        if (won == 0)
            return nominatorIsDirector || !nomineeActive ? Outcome.Conflict() : Outcome.Conflict(ProblemCodes.NominationNominatorNotDirector);

        // D-014: a antiga diretora fica sem nenhum cargo. D-158: o novo diretor deixa secretário e tesoureiro e mantém os demais.
        await roles.EndAllAsync(nomination.NominatorMembershipId, EndCause.DirectorTransferred, actor.PersonId, ct);
        await roles.EndAsync(nomination.NomineeMembershipId, role => role is RoleCode.Secretary or RoleCode.Treasurer, EndCause.BecameDirector, actor.PersonId, ct);
        var directorId = Guid.CreateVersion7();
        db.RoleAssignments.Add(new RoleAssignment
        {
            Id = directorId, ClubId = clubId, MembershipId = nomination.NomineeMembershipId, Role = RoleCode.Director,
            AssignedByPersonId = actor.PersonId, StartedAt = now,
        });
        audit.Record(AuditAction.DirectorChanged, AuditTarget.DirectorNomination, nominationId, clubId, AuditScope.Club, actor.PersonId);
        audit.Record(AuditAction.RoleAssigned, AuditTarget.RoleAssignment, directorId, clubId, AuditScope.Club, actor.PersonId);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (DbErrors.IsUniqueViolation(e, DbConstraints.UniqueClubRoles))
        {
            return Outcome.Conflict();
        }

        // Admin e diretoria vigente, já com a nova direção (D-145).
        var recipients = (await notifier.PlatformAdminsAsync(ct)).Concat(await notifier.BoardAsync(ct));
        notifier.Notify(NotificationEvent.DirectorChanged, directorId, clubId, recipients);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Outcome.Done;
    }

    public async Task<Outcome> DeclineAsync(Guid nominationId, CancellationToken ct)
    {
        var actor = await actors.GetClubActorAsync(ct);
        if (actor is null)
            return Outcome.NotFound;
        var nomination = await db.DirectorNominations.AsNoTracking().SingleOrDefaultAsync(n => n.Id == nominationId && n.ClubId == context.ClubId, ct);
        if (nomination is null)
            return Outcome.NotFound;
        if (nomination.NomineeMembershipId != actor.MembershipId)
            return Outcome.Forbidden();
        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var won = await db.DirectorNominations
            .Where(n => n.Id == nominationId && n.Status == NominationStatus.Pending && n.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, NominationStatus.Declined).SetProperty(n => n.ClosedAt, now), ct);
        if (won == 0)
            return Outcome.Conflict();
        audit.Record(AuditAction.DirectorNominationDeclined, AuditTarget.DirectorNomination, nominationId, context.ClubId, AuditScope.Club, actor.PersonId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Outcome.Done;
    }

    private static NominationResponse ToResponse(DirectorNomination n) =>
        new(n.Id, n.NominatorMembershipId, n.NomineeMembershipId, n.Status, n.ExpiresAt);
}
