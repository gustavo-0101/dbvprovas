using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Tenancy;

// D-119
public static class ClubEndpoints
{
    public static void MapClubs(this IEndpointRouteBuilder app)
    {
        var club = app.MapGroup("/api/clubs/{clubId:guid}")
            .RequireAuthorization(Policies.Member)
            .AddEndpointFilter<ClubMembershipFilter>()
            .WithTags("Clubs");
        club.MapGet("", GetClubAsync);
        club.MapGet("/members", ListMembersAsync);
        club.MapGet("/members/{membershipId:guid}", GetMemberAsync);
    }

    private static async Task<Results<Ok<ClubResponse>, NotFound>> GetClubAsync(Guid clubId, AppDbContext db, CancellationToken ct)
    {
        var club = await db.Clubs.Where(c => c.Id == clubId).Select(c => new ClubResponse(c.Id, c.Name)).SingleOrDefaultAsync(ct);
        return club is null ? TypedResults.NotFound() : TypedResults.Ok(club);
    }

    private static async Task<Ok<List<MemberResponse>>> ListMembersAsync(Guid clubId, AppDbContext db, CancellationToken ct)
    {
        var members = await (
            from m in db.Memberships
            where m.ClubId == clubId && m.EndedAt == null
            join p in db.Persons on m.PersonId equals p.Id
            orderby p.Name
            select new MemberResponse(m.Id, p.Name)).ToListAsync(ct);
        return TypedResults.Ok(members);
    }

    private static async Task<Results<Ok<MemberResponse>, NotFound>> GetMemberAsync(Guid clubId, Guid membershipId, AppDbContext db, CancellationToken ct)
    {
        var member = await (
            from m in db.Memberships
            where m.Id == membershipId && m.ClubId == clubId && m.EndedAt == null
            join p in db.Persons on m.PersonId equals p.Id
            select new MemberResponse(m.Id, p.Name)).SingleOrDefaultAsync(ct);
        return member is null ? TypedResults.NotFound() : TypedResults.Ok(member);
    }
}
