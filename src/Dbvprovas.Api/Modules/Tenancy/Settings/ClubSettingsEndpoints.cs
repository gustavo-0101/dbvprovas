using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Tenancy.Settings;

public static class ClubSettingsEndpoints
{
    public static void MapClubSettings(this IEndpointRouteBuilder app)
    {
        var club = app.MapGroup("/api/clubs/{clubId:guid}").RequireAuthorization(Policies.Member)
            .AddEndpointFilter<ClubMembershipFilter>().WithTags("Clubs");
        club.MapGet("/settings", async (ClubSettingsService service, CancellationToken ct) => (await service.GetAsync(ct)).ToHttp());
        club.MapPut("/settings", async (ClubSettingsRequest body, ClubSettingsService service, CancellationToken ct) =>
            (await service.SaveAsync(body, ct)).ToHttp());
        club.MapPost("/invite-code", async (ClubSettingsService service, CancellationToken ct) => (await service.RotateInviteAsync(ct)).ToHttp());
    }
}
