using System.Net;
using System.Net.Http.Json;
using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Tenancy;

public sealed class ClubIsolationApiTests(PostgresFixture db)
{
    [Fact]
    public async Task CA_TEN_001_Own_club_returns_its_name()
    {
        await using var api = new ApiFactory(db, "Development");
        using var client = api.CreateClientWithToken(await api.SignInAsync(DevSeed.AuroraAccount));

        var club = await client.GetFromJsonAsync<ClubResponse>(ApiRoutes.Club(DevSeed.ClubA));

        Assert.Equal(new ClubResponse(DevSeed.ClubA, "Clube Águias"), club);
    }

    [Fact]
    public async Task CA_TEN_001_Member_list_returns_only_own_club()
    {
        await using var api = new ApiFactory(db, "Development");
        using var client = api.CreateClientWithToken(await api.SignInAsync(DevSeed.AuroraAccount));

        var members = await client.GetFromJsonAsync<List<MemberResponse>>(ApiRoutes.Members(DevSeed.ClubA));

        Assert.NotNull(members);
        Assert.Equal(DevSeed.ClubAMemberNames, members.Select(m => m.Name));
    }

    [Fact]
    public async Task CA_TEN_001_Own_club_member_by_id_returns_member()
    {
        await using var api = new ApiFactory(db, "Development");
        using var client = api.CreateClientWithToken(await api.SignInAsync(DevSeed.AuroraAccount));

        var member = await client.GetFromJsonAsync<MemberResponse>(ApiRoutes.Member(DevSeed.ClubA, DevSeed.CaioMembership));

        Assert.Equal(new MemberResponse(DevSeed.CaioMembership, "Caio"), member);
    }

    [Fact]
    public async Task CA_TEN_001_Other_club_member_by_id_returns_same_404_as_missing_member()
    {
        await using var api = new ApiFactory(db, "Development");
        using var client = api.CreateClientWithToken(await api.SignInAsync(DevSeed.AuroraAccount));

        using var otherMember = await client.GetAsync(ApiRoutes.Member(DevSeed.ClubA, DevSeed.ElisaMembership));
        using var missingMember = await client.GetAsync(ApiRoutes.Member(DevSeed.ClubA, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, otherMember.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingMember.StatusCode);
        Assert.Equal(await missingMember.Content.ReadAsStringAsync(), await otherMember.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CA_TEN_001_Other_club_route_returns_same_404_as_missing_club()
    {
        await using var api = new ApiFactory(db, "Development");
        using var client = api.CreateClientWithToken(await api.SignInAsync(DevSeed.AuroraAccount));

        using var otherClub = await client.GetAsync(ApiRoutes.Club(DevSeed.ClubB));
        using var missingClub = await client.GetAsync(ApiRoutes.Club(Guid.NewGuid()));
        using var notAGuid = await client.GetAsync("api/clubs/nao-e-um-guid");

        Assert.All(new[] { otherClub, missingClub, notAGuid }, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        Assert.Equal(await missingClub.Content.ReadAsStringAsync(), await otherClub.Content.ReadAsStringAsync());
        Assert.Equal(await missingClub.Content.ReadAsStringAsync(), await notAGuid.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CA_TEN_001_Concurrent_requests_never_mix_clubs()
    {
        // Pool pequeno força o reuso das conexões entre clubes (RNF-TEN-001).
        await using var api = new ApiFactory(db, "Development", db.AppConnectionString + ";Maximum Pool Size=4");
        var aurora = await api.SignInAsync(DevSeed.AuroraAccount);
        var bento = await api.SignInAsync(DevSeed.BentoAccount);

        var results = await Task.WhenAll(Enumerable.Range(0, 100).Select(async i =>
        {
            var (token, club, expected) = i % 2 == 0
                ? (aurora, DevSeed.ClubA, DevSeed.ClubAMemberNames)
                : (bento, DevSeed.ClubB, DevSeed.ClubBMemberNames);
            using var client = api.CreateClientWithToken(token);
            var members = await client.GetFromJsonAsync<List<MemberResponse>>(ApiRoutes.Members(club));
            Assert.NotNull(members);
            return members.Select(m => m.Name).SequenceEqual(expected);
        }));

        Assert.All(results, ok => Assert.True(ok));
    }
}
