using System.Net;
using System.Net.Http.Json;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Tenancy;

public sealed class MembershipAccessTests(PostgresFixture db)
{
    [Fact]
    public async Task CA_TEN_008_Ended_membership_loses_access_on_next_request()
    {
        var member = await TestData.CreateClubWithMemberAsync(db, "Gilda");
        await using var api = new ApiFactory(db, "Development");
        using var client = api.CreateClientWithToken(await api.SignInAsync(member.AccountId));

        using var before = await client.GetAsync(ApiRoutes.Members(member.ClubId));
        await TestData.EndMembershipAsync(db, member.MembershipId);
        using var after = await client.GetAsync(ApiRoutes.Members(member.ClubId));

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }

    // RF-TEN-001: a própria conta continua visível sem participação ativa.
    [Fact]
    public async Task Me_without_active_membership_lists_no_clubs()
    {
        var member = await TestData.CreateClubWithMemberAsync(db, "Heitor");
        await TestData.EndMembershipAsync(db, member.MembershipId);
        await using var api = new ApiFactory(db, "Development");
        using var client = api.CreateClientWithToken(await api.SignInAsync(member.AccountId));

        var me = await client.GetFromJsonAsync<MeResponse>(ApiRoutes.Me);

        Assert.NotNull(me);
        Assert.Equal("Heitor", me.Name);
        Assert.Empty(me.Clubs);
    }
}
