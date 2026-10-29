using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Audit;
using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Notifications;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Organization;

// O admin nomeia diretor quando falta um e substitui o ausente (RF-ORG-005, PERM-ORG-005, RN-ORG-009, D-150, D-158).
// Tarefa própria: entra no clube escolhido pela troca explícita, vê só nome e maioridade, e cada leitura é auditada (RN-AUD-006).
public sealed class AdminDirectorService(
    AppDbContext db, ClubContext context, ClubScope scope, RoleTermination roles, AuditLog audit, Notifier notifier, TimeProvider clock)
{
    public async Task<Outcome<List<AdminClubResponse>>> ListClubsAsync(string? query, CancellationToken ct)
    {
        var clubs = db.AdminClubOverview.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var text = TextInput.Clean(query, 60);
            if (text is null)
                return Outcome.Invalid(ProblemCodes.ValidationText);
            var pattern = "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            clubs = clubs.Where(c => EF.Functions.ILike(c.Name, pattern, "\\") || EF.Functions.ILike(c.City, pattern, "\\"));
        }

        var list = await clubs.OrderBy(c => c.Name).Take(50).Select(c => new AdminClubResponse(c.Id, c.Name, c.City, c.HasDirector)).ToListAsync(ct);
        return Outcome<List<AdminClubResponse>>.Ok(list);
    }

    public async Task<Outcome<List<DirectorCandidateResponse>>> CandidatesAsync(Guid clubId, CancellationToken ct)
    {
        if (!await db.AdminClubOverview.AnyAsync(c => c.Id == clubId, ct))
            return Outcome.NotFound;
        await scope.EnterAsync(clubId, ct);
        var today = BrasiliaCalendar.Today(clock);
        var rows = await (
            from m in db.Memberships
            where m.ClubId == clubId && m.Status == MembershipStatus.Active
            join p in db.Persons on m.PersonId equals p.Id
            orderby p.Name
            select new { m.Id, m.PersonId, p.Name, p.BirthDate }).ToListAsync(ct);
        foreach (var row in rows)
            audit.Record(AuditAction.AdminPersonalDataRead, AuditTarget.Person, row.PersonId, clubId, AuditScope.Platform, context.PersonId,
                fields: [AdminDataField.AdultProfile]);
        await db.SaveChangesAsync(ct);
        return Outcome<List<DirectorCandidateResponse>>.Ok(rows.Select(r => new DirectorCandidateResponse(r.Id, r.Name ?? string.Empty,
            r.BirthDate is { } birth && AgeCalculator.AgeOn(birth, today) >= 18)).ToList());
    }

    public Task<Outcome> AppointAsync(Guid clubId, AppointDirectorRequest body, CancellationToken ct) =>
        ChangeAsync(clubId, body.MembershipId, null, ct);

    public Task<Outcome> ReplaceAsync(Guid clubId, ReplaceDirectorRequest body, CancellationToken ct) =>
        ReasonCodes.IsAllowed(ReasonOperation.ReplaceDirector, body.Reason)
            ? ChangeAsync(clubId, body.MembershipId, body.Reason, ct)
            : Task.FromResult(Outcome.Invalid(ProblemCodes.ReasonRequired));

    // reason nulo: nomeação (o clube não tem diretor); com motivo: substituição (encerra a atribuição anterior na mesma operação).
    private async Task<Outcome> ChangeAsync(Guid clubId, Guid targetMembershipId, ReasonCode? reason, CancellationToken ct)
    {
        var adminId = context.PersonId!.Value;
        if (!await db.AdminClubOverview.AnyAsync(c => c.Id == clubId, ct))
            return Outcome.NotFound;
        await scope.EnterAsync(clubId, ct);
        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // O filtro também deixa ver os cargos do próprio admin em outro clube: o clube vai no WHERE.
        var current = await db.RoleAssignments.AsNoTracking()
            .Where(r => r.ClubId == clubId && r.Role == RoleCode.Director && r.EndedAt == null).Select(r => (Guid?)r.MembershipId).SingleOrDefaultAsync(ct);
        if (reason is null && current is not null)
            return Outcome.Conflict(ProblemCodes.DirectorExists);
        if (reason is not null && current is null)
            return Outcome.Conflict(ProblemCodes.DirectorMissing);
        if (current == targetMembershipId)
            return Outcome.Forbidden(ProblemCodes.DirectorTargetIneligible);

        await RowLocks.LockMembershipsAsync(db, current is { } old ? [old, targetMembershipId] : [targetMembershipId], ct);
        var target = await (
            from m in db.Memberships
            where m.Id == targetMembershipId && m.ClubId == clubId
            join p in db.Persons on m.PersonId equals p.Id
            select new { m.Status, m.PersonId, p.BirthDate }).SingleOrDefaultAsync(ct);
        if (target is null)
            return Outcome.NotFound;
        if (target.PersonId == adminId)
            return Outcome.Forbidden(ProblemCodes.SelfAction);
        if (target.Status != MembershipStatus.Active)
            return Outcome.Forbidden(ProblemCodes.DirectorTargetIneligible);
        if (target.BirthDate is not { } birth || AgeCalculator.AgeOn(birth, BrasiliaCalendar.Today(clock)) < 18)
            return Outcome.Forbidden(ProblemCodes.AgeBelowMinimum);

        if (current is { } previous)
            await roles.EndAsync(previous, role => role == RoleCode.Director, EndCause.DirectorReplaced, adminId, ct);
        await roles.EndAsync(targetMembershipId, role => role is RoleCode.Secretary or RoleCode.Treasurer, EndCause.BecameDirector, adminId, ct);
        var directorId = Guid.CreateVersion7();
        db.RoleAssignments.Add(new RoleAssignment
        {
            Id = directorId, ClubId = clubId, MembershipId = targetMembershipId, Role = RoleCode.Director, AssignedByPersonId = adminId, StartedAt = now,
        });
        audit.Record(reason is null ? AuditAction.DirectorAppointed : AuditAction.DirectorReplaced, AuditTarget.RoleAssignment, directorId, clubId,
            AuditScope.Platform, adminId, reason: reason);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (DbErrors.IsUniqueViolation(e, DbConstraints.UniqueClubRoles))
        {
            return Outcome.Conflict();
        }

        var recipients = (await notifier.PlatformAdminsAsync(ct)).Concat(await notifier.BoardAsync(ct));
        notifier.Notify(NotificationEvent.DirectorChanged, directorId, clubId, recipients);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Outcome.Done;
    }
}
