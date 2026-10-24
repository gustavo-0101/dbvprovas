using System.Net;
using System.Net.Http.Json;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Shared;

public sealed class MeTests(PostgresFixture db)
{
    // RF-TEN-001, PC-09: "quem sou eu" traz o clube ativo com os cargos vigentes, o cadastro e o papel de admin.
    [Fact]
    public async Task Me_lists_active_club_with_roles_and_profile_flags()
    {
        var a = await TestData.CreateClubAAsync(db);
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = new ApiFactory(db, "Production") { UseTestAuthentication = true };

        var aurora = await api.CreateClientAs(a.Aurora).GetFromJsonAsync<MeResponse>(ApiRoutes.Me);
        var alfa = await api.CreateClientAs(admin).GetFromJsonAsync<MeResponse>(ApiRoutes.Me);

        Assert.NotNull(aurora);
        var club = Assert.Single(aurora.Clubs);
        Assert.Equal(a.ClubId, club.Id);
        Assert.Equal(a.Aurora.MembershipId, club.MembershipId);
        Assert.Equal(new[] { RoleCode.Director }, club.Roles);
        Assert.True(aurora.ProfileComplete);
        Assert.True(aurora.IsAdult);
        Assert.False(aurora.IsPlatformAdmin);
        Assert.NotNull(alfa);
        Assert.Empty(alfa.Clubs);
        Assert.True(alfa.IsPlatformAdmin);
    }

    // RN-AUD-005, PC-09: tirar a marca de admin vale no pedido seguinte; D-146: menor de 18 nunca é admin.
    [Fact]
    public async Task Me_reads_admin_flag_on_each_request_and_only_for_adults()
    {
        var admin = await TestData.CreateAdminAsync(db);
        var minor = await TestData.CreatePersonAsync(db, "Menor Marcado", age: 17, isPlatformAdmin: true);
        await using var api = new ApiFactory(db, "Production") { UseTestAuthentication = true };
        using var client = api.CreateClientAs(admin);

        var before = await client.GetFromJsonAsync<MeResponse>(ApiRoutes.Me);
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, "UPDATE accounts SET is_platform_admin = false WHERE id = @a", ("a", admin.AccountId));
        var after = await client.GetFromJsonAsync<MeResponse>(ApiRoutes.Me);
        var minorMe = await api.CreateClientAs(minor).GetFromJsonAsync<MeResponse>(ApiRoutes.Me);

        Assert.True(before!.IsPlatformAdmin);
        Assert.False(after!.IsPlatformAdmin);
        Assert.False(minorMe!.IsPlatformAdmin);
    }

    // CA-TEN-008, D-155: participação suspensa perde o clube no pedido seguinte.
    [Fact]
    public async Task CA_TEN_008_Suspended_membership_loses_access_on_next_request()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = new ApiFactory(db, "Production") { UseTestAuthentication = true };
        using var client = api.CreateClientAs(a.Iara);

        using var before = await client.GetAsync(ApiRoutes.Club(a.ClubId));
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString,
            "UPDATE memberships SET status = 'Suspended', suspended_at = now() WHERE id = @m", ("m", a.Iara.MembershipId));
        using var after = await client.GetAsync(ApiRoutes.Club(a.ClubId));
        var me = await client.GetFromJsonAsync<MeResponse>(ApiRoutes.Me);

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
        Assert.Empty(me!.Clubs);
    }

    // RF-ID-005: cadastro sem nascimento é incompleto e não conta como adulto.
    [Fact]
    public async Task Me_reports_incomplete_profile()
    {
        var person = await TestData.CreatePersonAsync(db, "Incompleta", age: null);
        await using var api = new ApiFactory(db, "Production") { UseTestAuthentication = true };

        var me = await api.CreateClientAs(person).GetFromJsonAsync<MeResponse>(ApiRoutes.Me);

        Assert.False(me!.ProfileComplete);
        Assert.False(me.IsAdult);
    }
}
