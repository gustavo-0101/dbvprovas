using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Identity;

public static class MeEndpoints
{
    public static void MapMe(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/me", GetMeAsync).RequireAuthorization(Policies.Member).WithTags("Identity");

    // RF-TEN-001, RF-ID-005, PC-09
    private static async Task<Results<Ok<MeResponse>, NotFound>> GetMeAsync(ClubContext context, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (context.PersonId is not { } personId)
            return TypedResults.NotFound();

        var person = await db.Persons.Where(p => p.Id == personId).Select(p => new { p.Name, p.BirthDate, p.Sex }).SingleOrDefaultAsync(ct);
        if (person is null)
            return TypedResults.NotFound();

        var memberships = await (
            from m in db.Memberships
            where m.PersonId == personId && m.Status == MembershipStatus.Active
            join c in db.Clubs on m.ClubId equals c.Id
            orderby c.Name
            select new { ClubId = c.Id, c.Name, MembershipId = m.Id }).ToListAsync(ct);
        var ids = memberships.Select(m => m.MembershipId).ToList();
        var roles = await db.RoleAssignments.Where(r => ids.Contains(r.MembershipId) && r.EndedAt == null)
            .Select(r => new { r.MembershipId, r.Role }).ToListAsync(ct);
        var clubs = memberships.Select(m => new ClubSummary(m.ClubId, m.Name, m.MembershipId,
            roles.Where(r => r.MembershipId == m.MembershipId).Select(r => r.Role).Order().ToList())).ToList();
        var complete = person is { Name: not null, BirthDate: not null, Sex: not null };
        var adult = person.BirthDate is { } birth && AgeCalculator.AgeOn(birth, BrasiliaCalendar.Today(clock)) >= 18;
        return TypedResults.Ok(new MeResponse(person.Name, clubs, complete, adult, context.IsPlatformAdmin));
    }
}
