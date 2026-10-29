using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Organization;

// Unidades (RF-ORG-001, PERM-ORG-006, RN-ORG-013, D-135, D-164). Faixa de 10 a 15 anos no ano (RN-ORG-018).
public sealed class UnitService(AppDbContext db, ClubContext context, ActorService actors, TimeProvider clock)
{
    public async Task<Outcome<List<UnitResponse>>> ListAsync(CancellationToken ct)
    {
        if (await AuthorizeAsync(ct) is { } denied)
            return denied;
        var units = await db.Units.OrderBy(u => u.Name).ThenBy(u => u.Id)
            .Select(u => new UnitResponse(u.Id, u.Name, u.Sex, u.MinAge, u.MaxAge, u.Status)).ToListAsync(ct);
        return Outcome<List<UnitResponse>>.Ok(units);
    }

    public async Task<Outcome<UnitResponse>> CreateAsync(UnitRequest body, CancellationToken ct)
    {
        if (await AuthorizeAsync(ct) is { } denied)
            return denied;
        if (Validate(body, out var name) is { } invalid)
            return invalid;
        var unit = new Unit
        {
            Id = Guid.CreateVersion7(), ClubId = context.ClubId!.Value, Name = name!, Sex = body.Sex, MinAge = body.MinAge, MaxAge = body.MaxAge,
            Status = body.Status, CreatedAt = clock.GetUtcNow(),
        };
        db.Units.Add(unit);
        await db.SaveChangesAsync(ct);
        return Outcome<UnitResponse>.Ok(ToResponse(unit));
    }

    public async Task<Outcome<UnitResponse>> UpdateAsync(Guid unitId, UnitRequest body, CancellationToken ct)
    {
        if (await AuthorizeAsync(ct) is { } denied)
            return denied;
        if (Validate(body, out var name) is { } invalid)
            return invalid;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Trava a unidade contra uma atribuição de conselheiro simultânea (D-164); sob RLS, unidade de outro clube não trava nem volta.
        await db.Database.ExecuteSqlAsync($"SELECT id FROM units WHERE id = {unitId} AND club_id = {context.ClubId} FOR UPDATE", ct);
        var unit = await db.Units.SingleOrDefaultAsync(u => u.Id == unitId && u.ClubId == context.ClubId, ct);
        if (unit is null)
            return Outcome.NotFound;
        if (unit.Sex != body.Sex)
        {
            // D-164: conselheiro vigente de outro sexo impede a mudança, sem retirar cargo em silêncio.
            var incompatible = await (
                from r in db.RoleAssignments
                where r.UnitId == unitId && r.EndedAt == null
                join m in db.Memberships on r.MembershipId equals m.Id
                join p in db.Persons on m.PersonId equals p.Id
                where p.Sex != body.Sex
                select r.Id).AnyAsync(ct);
            if (incompatible)
                return Outcome.Conflict(ProblemCodes.UnitSexConflict);
        }

        unit.Name = name!;
        unit.Sex = body.Sex;
        unit.MinAge = body.MinAge;
        unit.MaxAge = body.MaxAge;
        unit.Status = body.Status;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Outcome<UnitResponse>.Ok(ToResponse(unit));
    }

    private static Outcome? Validate(UnitRequest body, out string? name)
    {
        name = TextInput.Clean(body.Name, 60);
        if (name is null)
            return Outcome.Invalid(ProblemCodes.ValidationText);
        if (!Enum.IsDefined(body.Sex) || !Enum.IsDefined(body.Status))
            return Outcome.Invalid(ProblemCodes.ValidationText);
        return body.MinAge is >= 10 and <= 15 && body.MaxAge is >= 10 and <= 15 && body.MinAge <= body.MaxAge
            ? null
            : Outcome.Invalid(ProblemCodes.UnitInvalidRange);
    }

    private async Task<Outcome?> AuthorizeAsync(CancellationToken ct)
    {
        var actor = await actors.GetClubActorAsync(ct);
        if (actor is null)
            return Outcome.NotFound;
        return actor.IsDirectorOrSecretary ? null : Outcome.Forbidden();
    }

    private static UnitResponse ToResponse(Unit u) => new(u.Id, u.Name, u.Sex, u.MinAge, u.MaxAge, u.Status);
}
