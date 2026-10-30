using System.Net;
using System.Net.Http.Json;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Organization;

public sealed class OrgIsolationTests(PostgresFixture db)
{
    private static IEnumerable<(HttpMethod Method, Func<Guid, string> Route, object? Body)> ClubRoutes()
    {
        var any = Guid.NewGuid();
        yield return (HttpMethod.Get, ApiRoutes.ClubSettings, null);
        yield return (HttpMethod.Put, ApiRoutes.ClubSettings, new ClubSettingsRequest("X", "Y", null));
        yield return (HttpMethod.Post, ApiRoutes.ClubInviteCode, null);
        yield return (HttpMethod.Get, ApiRoutes.Units, null);
        yield return (HttpMethod.Post, ApiRoutes.Units, new UnitRequest("U", Sex.Female, 10, 12));
        yield return (HttpMethod.Put, c => ApiRoutes.Unit(c, any), new UnitRequest("U", Sex.Female, 10, 12));
        yield return (HttpMethod.Get, ApiRoutes.Leaders, null);
        yield return (HttpMethod.Post, ApiRoutes.RoleAssignments, new AssignRoleRequest(any, RoleCode.Chaplain, null));
        yield return (HttpMethod.Post, c => ApiRoutes.EndRoleAssignment(c, any), null);
        yield return (HttpMethod.Post, ApiRoutes.DirectorNominations, new NominateDirectorRequest(any));
        yield return (HttpMethod.Get, ApiRoutes.CurrentDirectorNomination, null);
        yield return (HttpMethod.Post, c => ApiRoutes.CancelDirectorNomination(c, any), null);
        yield return (HttpMethod.Post, c => ApiRoutes.AcceptDirectorNomination(c, any), null);
        yield return (HttpMethod.Post, c => ApiRoutes.DeclineDirectorNomination(c, any), null);
        yield return (HttpMethod.Post, c => ApiRoutes.SuspendMember(c, any), new MemberStatusRequest(ReasonCode.TemporaryLeave));
        yield return (HttpMethod.Post, c => ApiRoutes.RemoveMember(c, any), new MemberStatusRequest(ReasonCode.AdministrativeClosure));
        yield return (HttpMethod.Post, c => ApiRoutes.ReactivateMember(c, any), new MemberStatusRequest(ReasonCode.ReturnAfterLeave));
    }

    private static async Task<(HttpStatusCode Status, string Body)> SendAsync(HttpClient client, HttpMethod method, string route, object? body)
    {
        using var request = new HttpRequestMessage(method, route) { Content = body is null ? null : JsonContent.Create(body) };
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private Task<string> SnapshotAsync(Guid clubId) => OwnerSql.ScalarAsync<string>(db.OwnerConnectionString, """
        SELECT concat_ws('|',
            (SELECT string_agg(name || sex || min_age || max_age || status, ',' ORDER BY id) FROM units WHERE club_id = @c),
            (SELECT string_agg(role || coalesce(ended_at::text, ''), ',' ORDER BY id) FROM role_assignments WHERE club_id = @c),
            (SELECT string_agg(status, ',' ORDER BY id) FROM memberships WHERE club_id = @c),
            (SELECT count(*)::text FROM director_nominations WHERE club_id = @c))
        """, ("c", clubId));

    // CA-TEN-001 a–c (R1), D-117: rota de outro clube e de clube inexistente dão o mesmo 404 vazio.
    [Fact]
    public async Task CA_TEN_001_Org_routes_of_other_club_return_the_same_404_as_missing()
    {
        var a = await TestData.CreateClubAAsync(db);
        var b = await TestData.CreateClubBAsync(db);
        await using var api = new ApiFactory(db, "Production") { UseTestAuthentication = true };
        using var aurora = api.CreateClientAs(a.Aurora);

        foreach (var (method, route, body) in ClubRoutes())
        {
            var other = await SendAsync(aurora, method, route(b.ClubId), body);
            var missing = await SendAsync(aurora, method, route(Guid.NewGuid()), body);
            Assert.Equal((HttpStatusCode.NotFound, string.Empty), other);
            Assert.Equal(other, missing);
        }
    }

    // CA-TEN-001 b (R1): na rota de A, IDs de B não aparecem nem mudam nada em B.
    [Fact]
    public async Task CA_TEN_001_Org_ids_of_other_club_in_own_route_are_invisible()
    {
        var a = await TestData.CreateClubAAsync(db);
        var b = await TestData.CreateClubBAsync(db);
        await using var api = new ApiFactory(db, "Production") { UseTestAuthentication = true };
        using var aurora = api.CreateClientAs(a.Aurora);
        var bentoDirector = await OwnerSql.ScalarAsync<Guid>(db.OwnerConnectionString,
            "SELECT id FROM role_assignments WHERE membership_id = @m", ("m", b.Bento.MembershipId));
        var before = await SnapshotAsync(b.ClubId);

        Assert.Equal(HttpStatusCode.NotFound, (await aurora.PutAsJsonAsync(ApiRoutes.Unit(a.ClubId, b.UnitZ), new UnitRequest("Z2", Sex.Male, 10, 12))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await aurora.PostAsync(ApiRoutes.EndRoleAssignment(a.ClubId, bentoDirector), null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await aurora.PostAsJsonAsync(ApiRoutes.SuspendMember(a.ClubId, b.Bento.MembershipId),
            new MemberStatusRequest(ReasonCode.TemporaryLeave))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await aurora.PostAsJsonAsync(ApiRoutes.DirectorNominations(a.ClubId),
            new NominateDirectorRequest(b.Bento.MembershipId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await aurora.PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId),
            new AssignRoleRequest(b.Bento.MembershipId, RoleCode.Chaplain, null))).StatusCode);
        Assert.Equal(before, await SnapshotAsync(b.ClubId));
    }
}
