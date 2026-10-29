using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Audit;
using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Organization;

// Liderança e cargos (RF-ORG-002, PERM-ORG-001, RN-ORG-001 a 006 e 017, D-133, D-161, D-170).
public sealed class RoleAssignmentService(
    AppDbContext db, ClubContext context, ActorService actors, RoleTermination roles, AuditLog audit, TimeProvider clock)
{
    public async Task<Outcome<List<LeaderResponse>>> ListLeadersAsync(CancellationToken ct)
    {
        if (await DirectorOrSecretaryAsync(ct) is not { } actor)
            return await DeniedAsync(ct);
        var leaders = await (
            from m in db.Memberships
            where m.ClubId == context.ClubId && m.Status != MembershipStatus.Ended
            join p in db.Persons on m.PersonId equals p.Id
            orderby p.Name, m.Id
            select new { m.Id, p.Name, m.Status }).ToListAsync(ct);
        var ids = leaders.Select(l => l.Id).ToList();
        var assignments = await db.RoleAssignments.Where(r => ids.Contains(r.MembershipId) && r.EndedAt == null)
            .Select(r => new { r.MembershipId, Role = new LeaderRoleResponse(r.Id, r.Role, r.UnitId) }).ToListAsync(ct);
        return Outcome<List<LeaderResponse>>.Ok(leaders.Select(l => new LeaderResponse(l.Id, l.Name ?? string.Empty, l.Status,
            assignments.Where(a => a.MembershipId == l.Id).Select(a => a.Role).OrderBy(r => r.Role).ToList())).ToList());
    }

    public async Task<Outcome<RoleAssignmentResponse>> AssignAsync(AssignRoleRequest body, CancellationToken ct)
    {
        if (await DirectorOrSecretaryAsync(ct) is not { } actor)
            return await DeniedAsync(ct);
        // Defesa além do conversor dos contratos: só os nove cargos fixos (RN-ORG-001).
        if (!Enum.IsDefined(body.Role))
            return Outcome.Invalid(ProblemCodes.RoleUnknown);
        if (body.Role == RoleCode.Director)
            return Outcome.Forbidden(ProblemCodes.RoleDirectorForbidden);
        if (body.MembershipId == actor.MembershipId)
            return Outcome.Forbidden(ProblemCodes.SelfAction);
        if (RoleRules.RequiresUnit(body.Role) && body.UnitId is null)
            return Outcome.Invalid(ProblemCodes.RoleUnitRequired);
        if (!RoleRules.RequiresUnit(body.Role) && body.UnitId is not null)
            return Outcome.Invalid(ProblemCodes.RoleUnitNotAllowed);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RowLocks.LockMembershipsAsync(db, [body.MembershipId], ct);
        var target = await (
            from m in db.Memberships
            where m.Id == body.MembershipId && m.ClubId == context.ClubId
            join p in db.Persons on m.PersonId equals p.Id
            select new { m.Status, m.Kind, p.BirthDate, p.Sex }).SingleOrDefaultAsync(ct);
        if (target is null)
            return Outcome.NotFound;
        if (target.Status != MembershipStatus.Active || target.Kind != MembershipKind.Leader)
            return Outcome.Forbidden(ProblemCodes.RoleTargetIneligible);
        if (target.BirthDate is not { } birth || AgeCalculator.AgeOn(birth, BrasiliaCalendar.Today(clock)) < RoleRules.MinimumAge(body.Role))
            return Outcome.Forbidden(ProblemCodes.AgeBelowMinimum);
        if (body.UnitId is { } unitId)
        {
            // Trava a unidade contra a mudança de sexo simultânea (D-164); a UnitService trava a mesma linha.
            await db.Database.ExecuteSqlAsync($"SELECT id FROM units WHERE id = {unitId} AND club_id = {context.ClubId} FOR SHARE", ct);
            var unit = await db.Units.Where(u => u.Id == unitId && u.ClubId == context.ClubId).Select(u => new { u.Sex, u.Status }).SingleOrDefaultAsync(ct);
            if (unit is null)
                return Outcome.NotFound;
            if (unit.Status != UnitStatus.Active)
                return Outcome.Forbidden(ProblemCodes.RoleUnitInactive);
            if (unit.Sex != target.Sex)
                return Outcome.Forbidden(ProblemCodes.RoleUnitSexMismatch);
        }

        var assignment = new RoleAssignment
        {
            Id = Guid.CreateVersion7(), ClubId = context.ClubId!.Value, MembershipId = body.MembershipId, Role = body.Role, UnitId = body.UnitId,
            AssignedByPersonId = actor.PersonId, StartedAt = clock.GetUtcNow(),
        };
        db.RoleAssignments.Add(assignment);
        audit.Record(AuditAction.RoleAssigned, AuditTarget.RoleAssignment, assignment.Id, assignment.ClubId, AuditScope.Club, actor.PersonId);
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException e) when (DbErrors.IsUniqueViolation(e, DbConstraints.UniqueClubRoles))
        {
            return Outcome.Conflict(ProblemCodes.RoleUniqueTaken);
        }
        catch (DbUpdateException e) when (DbErrors.IsUniqueViolation(e, DbConstraints.OneAssignmentPerRole))
        {
            return Outcome.Conflict(ProblemCodes.RoleAlreadyAssigned);
        }

        return Outcome<RoleAssignmentResponse>.Ok(new RoleAssignmentResponse(assignment.Id, assignment.MembershipId, assignment.Role, assignment.UnitId));
    }

    // D-161: retirada corretiva sem exigir a elegibilidade atual do alvo; ator autorizado, mesmo clube, nunca a si e nunca o diretor.
    public async Task<Outcome> EndAsync(Guid assignmentId, CancellationToken ct)
    {
        if (await DirectorOrSecretaryAsync(ct) is not { } actor)
            return await DeniedAsync(ct);
        var assignment = await db.RoleAssignments.AsNoTracking()
            .Where(r => r.Id == assignmentId && r.ClubId == context.ClubId).SingleOrDefaultAsync(ct);
        if (assignment is null)
            return Outcome.NotFound;
        if (assignment.Role == RoleCode.Director)
            return Outcome.Forbidden(ProblemCodes.RoleDirectorForbidden);
        if (assignment.MembershipId == actor.MembershipId)
            return Outcome.Forbidden(ProblemCodes.SelfAction);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RowLocks.LockMembershipsAsync(db, [assignment.MembershipId], ct);
        if (!await db.RoleAssignments.AnyAsync(r => r.Id == assignmentId && r.ClubId == context.ClubId && r.EndedAt == null, ct))
            return Outcome.Conflict();
        var ended = await roles.EndAsync(assignment.MembershipId, role => role == assignment.Role, EndCause.RemovedByBoard, actor.PersonId, ct);
        if (ended == 0)
            return Outcome.Conflict();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Outcome.Done;
    }

    private async Task<ClubActor?> DirectorOrSecretaryAsync(CancellationToken ct)
    {
        var actor = await actors.GetClubActorAsync(ct);
        return actor is { IsDirectorOrSecretary: true } ? actor : null;
    }

    private async Task<Outcome> DeniedAsync(CancellationToken ct) =>
        await actors.GetClubActorAsync(ct) is null ? Outcome.NotFound : Outcome.Forbidden();
}
