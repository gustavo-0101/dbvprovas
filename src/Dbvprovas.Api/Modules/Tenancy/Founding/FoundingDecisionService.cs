using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Audit;
using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Notifications;
using Dbvprovas.Api.Modules.Organization;
using Dbvprovas.Api.Modules.Privacy;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Tenancy.Founding;

// Decisão do admin sobre a fundação (RF-TEN-006, PERM-TEN-002, RN-TEN-002 a 004, D-137, D-157). Tarefa própria do
// admin: vê só o mínimo do solicitante, e cada leitura é auditada (RN-AUD-006, CA-AUD-008).
public sealed class FoundingDecisionService(
    AppDbContext db, ClubContext context, ClubScope scope, RoleTermination roles, AuditLog audit, Notifier notifier, TimeProvider clock)
{
    private sealed class RequesterState
    {
        public Guid? DirectorClubId { get; init; }
        public Guid? MembershipId { get; init; }
        public Guid? MembershipClubId { get; init; }
    }

    public async Task<List<AdminFoundingRequestResponse>> ListPendingAsync(CancellationToken ct)
    {
        var adminId = context.PersonId!.Value;
        var today = BrasiliaCalendar.Today(clock);
        var rows = await (
            from f in db.FoundingRequests
            where f.Status == FoundingRequestStatus.Pending
            join p in db.Persons on f.RequesterPersonId equals p.Id
            orderby f.CreatedAt
            select new
            {
                Request = f, p.Name, p.BirthDate,
                FoundedBefore = db.FoundingRecords.Any(r => r.PersonId == f.RequesterPersonId),
            }).ToListAsync(ct);
        foreach (var row in rows)
            audit.Record(AuditAction.AdminPersonalDataRead, AuditTarget.Person, row.Request.RequesterPersonId, null, AuditScope.Platform, adminId,
                fields: [AdminDataField.AdultProfile]);
        await db.SaveChangesAsync(ct);
        return rows.Select(r => new AdminFoundingRequestResponse(r.Request.Id, r.Request.ClubName, r.Request.ClubCity, r.Request.ClubChurch,
            r.Request.ExceptionRequested, r.Name ?? string.Empty, r.BirthDate is { } birth && AgeCalculator.AgeOn(birth, today) >= 18,
            r.FoundedBefore, r.Request.CreatedAt)).ToList();
    }

    public async Task<Outcome<FoundingApprovedResponse>> ApproveAsync(Guid requestId, ApproveFoundingRequest body, CancellationToken ct)
    {
        var adminId = context.PersonId!.Value;
        if (body.GrantException && !ReasonCodes.IsAllowed(ReasonOperation.FoundingException, body.ExceptionReason))
            return Outcome.Invalid(ProblemCodes.ReasonRequired);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var request = await db.FoundingRequests.AsNoTracking().SingleOrDefaultAsync(f => f.Id == requestId, ct);
        if (request is null)
            return Outcome.NotFound;
        if (request.RequesterPersonId == adminId)
            return Outcome.Forbidden(ProblemCodes.FoundingOwnRequest);
        if (request.Status != FoundingRequestStatus.Pending)
            return Outcome.Conflict();

        // Condições revalidadas na decisão (RN-TEN-003): 18+, cadastro completo, não diretor, fundação anterior × exceção.
        // O admin só vê o solicitante enquanto o pedido está pendente; sem ele, outra decisão já venceu (D-140).
        var person = await db.Persons.Where(p => p.Id == request.RequesterPersonId).Select(p => new { p.Name, p.BirthDate, p.Sex }).SingleOrDefaultAsync(ct);
        if (person is null)
            return Outcome.Conflict();
        if (person.Name is null || person.BirthDate is not { } birth || person.Sex is null)
            return Outcome.Forbidden(ProblemCodes.ProfileIncomplete);
        if (AgeCalculator.AgeOn(birth, BrasiliaCalendar.Today(clock)) < 18)
            return Outcome.Forbidden(ProblemCodes.AgeBelowMinimum);
        var foundedBefore = await db.FoundingRecords.AnyAsync(r => r.PersonId == request.RequesterPersonId, ct);
        var states = await db.Database.SqlQuery<RequesterState>(
                $"SELECT director_club_id AS \"DirectorClubId\", membership_id AS \"MembershipId\", membership_club_id AS \"MembershipClubId\" FROM founding_requester_state({requestId})")
            .ToListAsync(ct);
        var consent = await db.Consents.SingleOrDefaultAsync(c => c.FoundingRequestId == requestId && c.RevokedAt == null && c.EndedAt == null, ct);
        // Uma decisão que venceu depois da primeira leitura muda a fundação anterior e o consentimento: quem perdeu recebe 409 (D-140).
        if (!await db.FoundingRequests.AnyAsync(f => f.Id == requestId && f.Status == FoundingRequestStatus.Pending, ct))
            return Outcome.Conflict();
        if (foundedBefore && !body.GrantException)
            return Outcome.Forbidden(ProblemCodes.FoundingExceptionRequired);
        if (!foundedBefore && body.GrantException)
            return Outcome.Invalid(ProblemCodes.FoundingExceptionNotApplicable);
        if (states.Any(s => s.DirectorClubId is not null))
            return Outcome.Forbidden(ProblemCodes.FoundingIsDirector);
        if (consent is null)
            return Outcome.Forbidden(ProblemCodes.ConsentRequired);
        // A aprovação grava o clube no consentimento: trava exclusiva, sem upgrade de SHARE (D-140).
        await RowLocks.LockConsentAsync(db, consent.Id, exclusive: true, ct);

        // A primeira transição válida vence (D-140).
        var now = clock.GetUtcNow();
        var won = await db.FoundingRequests.Where(f => f.Id == requestId && f.Status == FoundingRequestStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.Status, FoundingRequestStatus.Approved)
                .SetProperty(f => f.DecidedByPersonId, adminId)
                .SetProperty(f => f.ExceptionGranted, body.GrantException)
                .SetProperty(f => f.ReasonCode, body.GrantException ? body.ExceptionReason : null)
                .SetProperty(f => f.ClosedAt, now), ct);
        if (won == 0)
            return Outcome.Conflict();

        // RN-TEN-004, D-137: as participações anteriores, ativa ou suspensa, se encerram com os cargos; os registros ficam no
        // clube antigo. Se uma delas mudou enquanto o admin decidia, nada é gravado: sem commit, a transação se desfaz.
        foreach (var previous in states.Where(s => s.MembershipId is not null))
        {
            var membership = previous.MembershipId!.Value;
            var previousClub = previous.MembershipClubId!.Value;
            await scope.EnterAsync(previousClub, ct);
            await RowLocks.LockMembershipsAsync(db, [membership], ct);
            var ended = await db.Memberships.Where(m => m.Id == membership && m.ClubId == previousClub
                    && (m.Status == MembershipStatus.Active || m.Status == MembershipStatus.Suspended))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, MembershipStatus.Ended)
                    .SetProperty(m => m.EndedAt, now)
                    .SetProperty(m => m.SuspendedAt, (DateTimeOffset?)null)
                    .SetProperty(m => m.StatusCause, EndCause.FoundedNewClub), ct);
            if (ended == 0)
                return Outcome.Conflict();
            await roles.EndAllAsync(membership, EndCause.MembershipEnded, adminId, ct);
            audit.Record(AuditAction.MembershipEnded, AuditTarget.Membership, membership, previousClub, AuditScope.Platform, adminId,
                cause: EndCause.FoundedNewClub);
            await db.SaveChangesAsync(ct);
        }

        var clubId = request.ProposedClubId;
        await scope.EnterAsync(clubId, ct);
        var membershipId = Guid.CreateVersion7();
        var directorId = Guid.CreateVersion7();
        db.Clubs.Add(new Club
        {
            Id = clubId, Name = request.ClubName, City = request.ClubCity, Church = request.ClubChurch, InviteCode = InviteCodes.New(),
            FoundingRequestId = request.Id, CreatedAt = now,
        });
        db.Memberships.Add(new Membership { Id = membershipId, ClubId = clubId, PersonId = request.RequesterPersonId, StartedAt = now });
        db.RoleAssignments.Add(new RoleAssignment
        {
            Id = directorId, ClubId = clubId, MembershipId = membershipId, Role = RoleCode.Director, AssignedByPersonId = adminId, StartedAt = now,
        });
        db.FoundingRecords.Add(new FoundingRecord { Id = Guid.CreateVersion7(), PersonId = request.RequesterPersonId, ClubId = clubId, ApprovedAt = now });
        consent.ClubId = clubId;
        audit.Record(AuditAction.FoundingApproved, AuditTarget.FoundingRequest, request.Id, clubId, AuditScope.Platform, adminId,
            reason: body.GrantException ? body.ExceptionReason : null);
        audit.Record(AuditAction.RoleAssigned, AuditTarget.RoleAssignment, directorId, clubId, AuditScope.Platform, adminId);
        notifier.Notify(NotificationEvent.FoundingApproved, request.Id, clubId, [request.RequesterPersonId]);
        notifier.QueueEmail(EmailTemplate.FoundingApproved, request.Id, request.RequesterAccountId);
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException e) when (DbErrors.IsUniqueViolation(e, DbConstraints.OneActiveMembershipPerPerson))
        {
            return Outcome.Conflict();
        }

        return Outcome<FoundingApprovedResponse>.Ok(new FoundingApprovedResponse(clubId));
    }

    public async Task<Outcome> RejectAsync(Guid requestId, RejectFoundingRequest body, CancellationToken ct)
    {
        var adminId = context.PersonId!.Value;
        if (!ReasonCodes.IsAllowed(ReasonOperation.RejectRequest, body.Reason))
            return Outcome.Invalid(ProblemCodes.ReasonRequired);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var request = await db.FoundingRequests.AsNoTracking().SingleOrDefaultAsync(f => f.Id == requestId, ct);
        if (request is null)
            return Outcome.NotFound;
        if (request.RequesterPersonId == adminId)
            return Outcome.Forbidden(ProblemCodes.FoundingOwnRequest);
        var consent = await db.Consents.SingleOrDefaultAsync(c => c.FoundingRequestId == requestId && c.RevokedAt == null && c.EndedAt == null, ct);
        if (consent is not null)
            await RowLocks.LockConsentAsync(db, consent.Id, exclusive: true, ct);
        var now = clock.GetUtcNow();
        var won = await db.FoundingRequests.Where(f => f.Id == requestId && f.Status == FoundingRequestStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.Status, FoundingRequestStatus.Rejected)
                .SetProperty(f => f.DecidedByPersonId, adminId)
                .SetProperty(f => f.ReasonCode, body.Reason)
                .SetProperty(f => f.ClosedAt, now), ct);
        if (won == 0)
            return Outcome.Conflict();
        await db.Consents.Where(c => c.FoundingRequestId == requestId && c.RevokedAt == null && c.EndedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.EndedAt, now).SetProperty(c => c.EndCause, EndCause.RequestClosed), ct);
        audit.Record(AuditAction.FoundingRejected, AuditTarget.FoundingRequest, requestId, null, AuditScope.Platform, adminId, reason: body.Reason);
        notifier.Notify(NotificationEvent.FoundingRejected, requestId, null, [request.RequesterPersonId]);
        notifier.QueueEmail(EmailTemplate.FoundingRejected, requestId, request.RequesterAccountId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Outcome.Done;
    }
}
