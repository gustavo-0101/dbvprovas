using System.Net;
using System.Net.Http.Json;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Organization;

public sealed class AdminDirectorTests(PostgresFixture db)
{
    private ApiFactory Api() => new(db, "Production") { UseTestAuthentication = true };

    private async Task EndDirectorAsync(Guid clubId) =>
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString,
            "UPDATE role_assignments SET ended_at = now() WHERE club_id = @c AND role = 'Director'", ("c", clubId));

    // a: sem diretor, o admin vê só nome e maioridade dos candidatos (leituras auditadas) e nomeia outro líder 18+;
    // a secretaria nomeada deixa a secretaria (D-158).
    [Fact]
    public async Task CA_ORG_006_Admin_appoints_when_club_has_no_director()
    {
        var a = await TestData.CreateClubAAsync(db);
        await EndDirectorAsync(a.ClubId);
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = Api();
        using var client = api.CreateClientAs(admin);

        var clubs = await client.GetFromJsonAsync<List<AdminClubResponse>>(ApiRoutes.AdminClubs + "?query=Águias");
        var candidates = await client.GetFromJsonAsync<List<DirectorCandidateResponse>>(ApiRoutes.AdminDirectorCandidates(a.ClubId));
        using var appointed = await client.PostAsJsonAsync(ApiRoutes.AdminAppointDirector(a.ClubId), new AppointDirectorRequest(a.Caio.MembershipId));
        var raw = await client.GetStringAsync(ApiRoutes.AdminDirectorCandidates(a.ClubId));

        Assert.Contains(clubs!, c => c.Id == a.ClubId && !c.HasDirector);
        Assert.Contains(candidates!, c => c.MembershipId == a.Caio.MembershipId && c.IsAdult);
        Assert.DoesNotContain("birthDate", raw);
        Assert.Equal(HttpStatusCode.NoContent, appointed.StatusCode);
        Assert.Equal("Director", await OwnerSql.ScalarAsync<string>(db.OwnerConnectionString,
            "SELECT string_agg(role, ',') FROM role_assignments WHERE membership_id = @m AND ended_at IS NULL", ("m", a.Caio.MembershipId)));
        Assert.True(await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM audit_entries WHERE club_id = @c AND action = 'AdminPersonalDataRead' AND scope = 'Platform'", ("c", a.ClubId)) >= 5);
    }

    // a, D-150, D-163: diretora ausente ainda registrada é substituída com motivo, na mesma operação; a indicação antiga cai.
    [Fact]
    public async Task CA_ORG_006_Admin_replaces_absent_director_atomically()
    {
        var a = await TestData.CreateClubAAsync(db);
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = Api();
        await api.CreateClientAs(a.Aurora).PostAsJsonAsync(ApiRoutes.DirectorNominations(a.ClubId), new NominateDirectorRequest(a.Iara.MembershipId));

        using var replaced = await api.CreateClientAs(admin).PostAsJsonAsync(ApiRoutes.AdminReplaceDirector(a.ClubId),
            new ReplaceDirectorRequest(a.Dalva.MembershipId, ReasonCode.DirectorUnavailable));

        Assert.Equal(HttpStatusCode.NoContent, replaced.StatusCode);
        Assert.Equal(a.Dalva.MembershipId, await OwnerSql.ScalarAsync<Guid>(db.OwnerConnectionString,
            "SELECT membership_id FROM role_assignments WHERE club_id = @c AND role = 'Director' AND ended_at IS NULL", ("c", a.ClubId)));
        Assert.Equal("Cancelled", await OwnerSql.ScalarAsync<string>(db.OwnerConnectionString,
            "SELECT status FROM director_nominations WHERE club_id = @c", ("c", a.ClubId)));
        Assert.Equal(1, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM audit_entries WHERE club_id = @c AND action = 'DirectorReplaced' AND reason_code = 'DirectorUnavailable'", ("c", a.ClubId)));
    }

    // b: não admin; c: nomeado de B para A; d: menor, inativo, a si mesmo, sem motivo, segundo diretor e substituir sem diretor.
    [Fact]
    public async Task CA_ORG_006_Invalid_appointments_are_denied()
    {
        var a = await TestData.CreateClubAAsync(db);
        var b = await TestData.CreateClubBAsync(db);
        var minor = await TestData.CreateMemberAsync(db, a.ClubId, "Menor", age: 17);
        var admin = await TestData.CreateAdminAsync(db);
        var adminMember = await TestData.AddMemberAsync(db, b.ClubId, admin);
        await using var api = Api();
        using var client = api.CreateClientAs(admin);

        Assert.Equal(HttpStatusCode.Forbidden, (await api.CreateClientAs(a.Caio).PostAsJsonAsync(ApiRoutes.AdminAppointDirector(a.ClubId), new AppointDirectorRequest(a.Iara.MembershipId))).StatusCode);
        await ProblemAssert.HasCodeAsync(await client.PostAsJsonAsync(ApiRoutes.AdminAppointDirector(a.ClubId), new AppointDirectorRequest(a.Iara.MembershipId)),
            HttpStatusCode.Conflict, ProblemCodes.DirectorExists);
        await ProblemAssert.HasCodeAsync(await client.PostAsJsonAsync(ApiRoutes.AdminReplaceDirector(a.ClubId), new ReplaceDirectorRequest(a.Iara.MembershipId, null)),
            HttpStatusCode.BadRequest, ProblemCodes.ReasonRequired);
        await EndDirectorAsync(a.ClubId);
        await EndDirectorAsync(b.ClubId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(ApiRoutes.AdminAppointDirector(a.ClubId), new AppointDirectorRequest(b.Bento.MembershipId))).StatusCode);
        await ProblemAssert.HasCodeAsync(await client.PostAsJsonAsync(ApiRoutes.AdminAppointDirector(a.ClubId), new AppointDirectorRequest(minor.MembershipId)),
            HttpStatusCode.Forbidden, ProblemCodes.AgeBelowMinimum);
        await ProblemAssert.HasCodeAsync(await client.PostAsJsonAsync(ApiRoutes.AdminAppointDirector(b.ClubId), new AppointDirectorRequest(adminMember.MembershipId)),
            HttpStatusCode.Forbidden, ProblemCodes.SelfAction);
        await ProblemAssert.HasCodeAsync(await client.PostAsJsonAsync(ApiRoutes.AdminReplaceDirector(a.ClubId), new ReplaceDirectorRequest(a.Iara.MembershipId, ReasonCode.DirectorUnavailable)),
            HttpStatusCode.Conflict, ProblemCodes.DirectorMissing);
    }

    // d: nomeações concorrentes conservam um diretor.
    [Fact]
    public async Task CA_ORG_006_Concurrent_appointments_keep_one_director()
    {
        var a = await TestData.CreateClubAAsync(db);
        await EndDirectorAsync(a.ClubId);
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = Api();
        using var client = api.CreateClientAs(admin);

        var results = await Task.WhenAll(new[] { a.Caio, a.Dalva, a.Iara, a.Lina }
            .Select(m => client.PostAsJsonAsync(ApiRoutes.AdminAppointDirector(a.ClubId), new AppointDirectorRequest(m.MembershipId))));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Equal(1, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM role_assignments WHERE club_id = @c AND role = 'Director' AND ended_at IS NULL", ("c", a.ClubId)));
        foreach (var result in results)
            result.Dispose();
    }
}
