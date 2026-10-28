using System.Net;
using System.Net.Http.Json;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Founding;

public sealed class ClubSettingsTests(PostgresFixture db)
{
    private ApiFactory Api() => new(db, "Production") { UseTestAuthentication = true };

    // a: diretora e secretário editam A e giram o convite; o código antigo deixa de valer para pedidos novos (D-162).
    [Fact]
    public async Task CA_TEN_011_Director_and_secretary_edit_club_and_rotate_invite()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);
        using var caio = api.CreateClientAs(a.Caio);

        var before = await aurora.GetFromJsonAsync<ClubSettingsResponse>(ApiRoutes.ClubSettings(a.ClubId));
        using var saved = await caio.PutAsJsonAsync(ApiRoutes.ClubSettings(a.ClubId), new ClubSettingsRequest("  Clube Águias Novo ", "Cidade Norte", "Igreja Nova"));
        var rotated = await (await aurora.PostAsync(ApiRoutes.ClubInviteCode(a.ClubId), null)).Content.ReadFromJsonAsync<ClubSettingsResponse>();
        var oldResolves = await OwnerSql.ScalarAsync<bool>(db.OwnerConnectionString, "SELECT club_id_by_invite(@c) IS NOT NULL", ("c", before!.InviteCode));
        var newResolves = await OwnerSql.ScalarAsync<bool>(db.OwnerConnectionString, "SELECT club_id_by_invite(@c) = @id", ("c", rotated!.InviteCode), ("id", a.ClubId));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("Clube Águias Novo", (await saved.Content.ReadFromJsonAsync<ClubSettingsResponse>())!.Name);
        Assert.NotEqual(before.InviteCode, rotated.InviteCode);
        Assert.False(oldResolves);
        Assert.True(newResolves);
    }

    // b: demais cargos não editam; c: dados de B pela sessão de A dão 404; d: cargo retirado ou ator suspenso perdem na sessão aberta.
    [Fact]
    public async Task CA_TEN_011_Other_roles_other_club_and_lost_authority_are_denied()
    {
        var a = await TestData.CreateClubAAsync(db);
        var b = await TestData.CreateClubBAsync(db);
        await using var api = Api();
        using var dalva = api.CreateClientAs(a.Dalva);
        using var aurora = api.CreateClientAs(a.Aurora);
        using var caio = api.CreateClientAs(a.Caio);
        var body = new ClubSettingsRequest("X", "Y", null);

        await ProblemAssert.HasCodeAsync(await dalva.PutAsJsonAsync(ApiRoutes.ClubSettings(a.ClubId), body), HttpStatusCode.Forbidden, ProblemCodes.PermissionDenied);
        Assert.Equal(HttpStatusCode.NotFound, (await aurora.PutAsJsonAsync(ApiRoutes.ClubSettings(b.ClubId), body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await aurora.PostAsync(ApiRoutes.ClubInviteCode(b.ClubId), null)).StatusCode);
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString,
            "UPDATE role_assignments SET ended_at = now() WHERE membership_id = @m", ("m", a.Caio.MembershipId));
        await ProblemAssert.HasCodeAsync(await caio.PostAsync(ApiRoutes.ClubInviteCode(a.ClubId), null), HttpStatusCode.Forbidden, ProblemCodes.PermissionDenied);
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString,
            "UPDATE memberships SET status = 'Suspended', suspended_at = now() WHERE id = @m", ("m", a.Aurora.MembershipId));
        Assert.Equal(HttpStatusCode.NotFound, (await aurora.GetAsync(ApiRoutes.ClubSettings(a.ClubId))).StatusCode);
    }
}
