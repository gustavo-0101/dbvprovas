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

    // RF-TEN-001
    private static async Task<Results<Ok<MeResponse>, NotFound>> GetMeAsync(ClubContext context, AppDbContext db, CancellationToken ct)
    {
        if (context.PersonId is not { } personId)
            return TypedResults.NotFound();

        var name = await db.Persons.Where(p => p.Id == personId).Select(p => p.Name).SingleOrDefaultAsync(ct);
        if (name is null)
            return TypedResults.NotFound();

        var clubs = await (
            from m in db.Memberships
            where m.PersonId == personId && m.EndedAt == null
            join c in db.Clubs on m.ClubId equals c.Id
            orderby c.Name
            select new ClubSummary(c.Id, c.Name)).ToListAsync(ct);
        return TypedResults.Ok(new MeResponse(name, clubs));
    }
}
