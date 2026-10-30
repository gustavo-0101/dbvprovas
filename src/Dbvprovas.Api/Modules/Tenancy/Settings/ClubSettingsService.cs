using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Organization;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Tenancy.Settings;

// Dados do clube e convite (RF-TEN-007, PERM-TEN-004, D-135, D-162): diretor e secretário do próprio clube.
public sealed class ClubSettingsService(AppDbContext db, ClubContext context, ActorService actors)
{
    public async Task<Outcome<ClubSettingsResponse>> GetAsync(CancellationToken ct)
    {
        if (await AuthorizeAsync(ct) is { } denied)
            return denied;
        var club = await db.Clubs.SingleAsync(c => c.Id == context.ClubId, ct);
        return Outcome<ClubSettingsResponse>.Ok(ToResponse(club));
    }

    public async Task<Outcome<ClubSettingsResponse>> SaveAsync(ClubSettingsRequest body, CancellationToken ct)
    {
        if (await AuthorizeAsync(ct) is { } denied)
            return denied;
        var name = TextInput.Clean(body.Name, 120);
        var city = TextInput.Clean(body.City, 120);
        var church = TextInput.Clean(body.Church, 120, required: false);
        if (name is null || city is null || church is null)
            return Outcome.Invalid(ProblemCodes.ValidationText);
        var club = await db.Clubs.SingleAsync(c => c.Id == context.ClubId, ct);
        club.Name = name;
        club.City = city;
        club.Church = church.Length == 0 ? null : church;
        await db.SaveChangesAsync(ct);
        return Outcome<ClubSettingsResponse>.Ok(ToResponse(club));
    }

    // D-162: o código novo vale para pedidos novos; os pedidos já enviados não guardam o código e continuam.
    public async Task<Outcome<ClubSettingsResponse>> RotateInviteAsync(CancellationToken ct)
    {
        if (await AuthorizeAsync(ct) is { } denied)
            return denied;
        var club = await db.Clubs.SingleAsync(c => c.Id == context.ClubId, ct);
        club.InviteCode = InviteCodes.New();
        await db.SaveChangesAsync(ct);
        return Outcome<ClubSettingsResponse>.Ok(ToResponse(club));
    }

    private async Task<Outcome?> AuthorizeAsync(CancellationToken ct)
    {
        var actor = await actors.GetClubActorAsync(ct);
        if (actor is null)
            return Outcome.NotFound;
        return actor.IsDirectorOrSecretary ? null : Outcome.Forbidden();
    }

    private static ClubSettingsResponse ToResponse(Club club) => new(club.Name, club.City, club.Church, club.InviteCode);
}
