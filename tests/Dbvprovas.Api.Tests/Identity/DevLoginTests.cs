using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Identity;

public sealed class DevLoginTests(PostgresFixture db)
{
    [Fact]
    public async Task CA_ID_002_Dev_login_lists_seed_accounts_and_switches_user()
    {
        await using var api = new ApiFactory(db, "Development");
        using var anonymous = api.CreateClient();

        var accounts = await anonymous.GetFromJsonAsync<List<DevAccountResponse>>(ApiRoutes.DevAccounts);
        var aurora = await api.SignInAsync(DevSeed.AuroraAccount);
        var bento = await api.SignInAsync(DevSeed.BentoAccount);
        using var auroraClient = api.CreateClientWithToken(aurora);
        using var bentoClient = api.CreateClientWithToken(bento);
        var meAurora = await auroraClient.GetFromJsonAsync<MeResponse>(ApiRoutes.Me);
        var meBento = await bentoClient.GetFromJsonAsync<MeResponse>(ApiRoutes.Me);

        Assert.Equal(new[] { "Aurora — Clube Águias", "Bento — Clube Corujas" }, accounts!.Select(a => a.Label));
        Assert.Equal("Aurora", meAurora!.Name);
        Assert.Equal(new[] { "Clube Águias" }, meAurora.Clubs.Select(c => c.Name));
        Assert.Equal("Bento", meBento!.Name);
        Assert.Equal(new[] { "Clube Corujas" }, meBento.Clubs.Select(c => c.Name));
    }

    // RF-ID-001
    [Fact]
    public async Task Dev_session_for_unknown_account_returns_404()
    {
        await using var api = new ApiFactory(db, "Development");
        using var client = api.CreateClient();

        using var response = await client.PostAsJsonAsync(ApiRoutes.DevSessions, new DevSessionRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CA_ID_001_Dev_login_does_not_exist_outside_development()
    {
        var empty = await db.CreateMigratedDatabaseAsync("dbv_ca_id_001");
        await using var dev = new ApiFactory(db, "Development");
        var devToken = await dev.SignInAsync(DevSeed.AuroraAccount);
        await using var production = new ApiFactory(db, "Production", empty.App);
        using var client = production.CreateClient();

        using var accounts = await client.GetAsync(ApiRoutes.DevAccounts);
        using var session = await client.PostAsJsonAsync(ApiRoutes.DevSessions, new DevSessionRequest(DevSeed.AuroraAccount));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", devToken);
        using var me = await client.GetAsync(ApiRoutes.Me);
        var clubs = await OwnerSql.ScalarAsync<long>(empty.Owner, "SELECT count(*) FROM clubs");

        Assert.Equal(HttpStatusCode.NotFound, accounts.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, session.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(0, clubs); // RF-TEN-003
    }
}
