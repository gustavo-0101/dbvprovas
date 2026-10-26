using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Organization;

public sealed record ActiveRole(Guid AssignmentId, RoleCode Role, Guid? UnitId);

// Quem age no clube da requisição: participação ativa, cargos vigentes e idade na data (PC-06, PC-09).
public sealed record ClubActor(Guid PersonId, Guid MembershipId, int? Age, Sex? Sex, IReadOnlyList<ActiveRole> Roles)
{
    public bool IsAdult => Age >= 18;

    public bool Has(RoleCode role) => Roles.Any(r => r.Role == role);

    // Valor fora dos nove cargos (linha antiga ou adulterada) não conta como cargo (RN-ORG-001).
    public bool HasAnyRole => Roles.Any(r => Enum.IsDefined(r.Role));

    // Diretoria (D-011): cargo vigente e 18 anos completos.
    public bool IsBoard => IsAdult && Roles.Any(r => RoleRules.IsBoard(r.Role));

    public bool IsDirectorOrSecretary => IsAdult && (Has(RoleCode.Director) || Has(RoleCode.Secretary));

    public bool IsDirector => IsAdult && Has(RoleCode.Director);
}

// Lido a cada requisição; nada vem da sessão (PC-09). Exige o clube fixado (ClubMembershipFilter ou troca explícita).
public sealed class ActorService(AppDbContext db, ClubContext context, TimeProvider clock)
{
    public async Task<ClubActor?> GetClubActorAsync(CancellationToken ct)
    {
        if (context.PersonId is not { } personId || context.ClubId is not { } clubId)
            return null;

        var membershipId = await db.Memberships
            .Where(m => m.ClubId == clubId && m.PersonId == personId && m.Status == MembershipStatus.Active)
            .Select(m => (Guid?)m.Id)
            .SingleOrDefaultAsync(ct);
        if (membershipId is not { } membership)
            return null;

        var person = await db.Persons.Where(p => p.Id == personId).Select(p => new { p.BirthDate, p.Sex }).SingleAsync(ct);
        var roles = await db.RoleAssignments
            .Where(r => r.MembershipId == membership && r.EndedAt == null)
            .Select(r => new ActiveRole(r.Id, r.Role, r.UnitId))
            .ToListAsync(ct);
        int? age = person.BirthDate is { } birth ? AgeCalculator.AgeOn(birth, BrasiliaCalendar.Today(clock)) : null;
        return new ClubActor(personId, membership, age, person.Sex, roles);
    }
}
