using System.Security.Claims;
using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Identity;

// D-120, RF-ID-003
public static class DevLoginEndpoints
{
    public static void MapDevLogin(this IEndpointRouteBuilder app)
    {
        var dev = app.MapGroup("/api/dev").AllowAnonymous().WithTags("Dev");
        dev.MapGet("/accounts", () =>
            TypedResults.Ok(DevSeed.Accounts.Select(a => new DevAccountResponse(a.AccountId, a.Label)).ToList()));
        dev.MapPost("/sessions", CreateSessionAsync).Produces<DevSessionResponse>();
    }

    private static async Task<Results<SignInHttpResult, NotFound>> CreateSessionAsync(
        DevSessionRequest request, AppDbContext db, CancellationToken ct)
    {
        var personId = await db.Accounts
            .Where(a => a.Id == request.AccountId)
            .Select(a => (Guid?)a.PersonId)
            .SingleOrDefaultAsync(ct);
        if (personId is null)
            return TypedResults.NotFound();

        var identity = new ClaimsIdentity(
            [new Claim(DbvClaims.AccountId, request.AccountId.ToString()), new Claim(DbvClaims.PersonId, personId.Value.ToString())],
            DevSession.Scheme);
        return TypedResults.SignIn(new ClaimsPrincipal(identity), authenticationScheme: DevSession.Scheme);
    }
}
