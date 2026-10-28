using System.Net;
using System.Net.Http.Json;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Organization;

public sealed class RoleAssignmentTests(PostgresFixture db)
{
    private ApiFactory Api() => new(db, "Production") { UseTestAuthentication = true };

    // RN-ORG-001: número no lugar do nome do cargo é recusado antes de qualquer regra, e nada é gravado.
    [Fact]
    public async Task Unknown_role_value_is_refused()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);

        using var response = await aurora.PostAsync(ApiRoutes.RoleAssignments(a.ClubId),
            JsonContent.Create(new { membershipId = a.Iara.MembershipId, role = 999, unitId = (Guid?)null }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM role_assignments WHERE membership_id = @m", ("m", a.Iara.MembershipId)));
    }

    // a: diretora atribui e retira cargo de outro líder; conselheira numa unidade do mesmo sexo; acúmulo e cargos não únicos.
    [Fact]
    public async Task CA_ORG_003_Director_assigns_and_ends_roles_of_other_leaders()
    {
        var a = await TestData.CreateClubAAsync(db);
        await TestData.CreateMemberAsync(db, a.ClubId, "Outra Capelã", role: RoleCode.Chaplain);
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);

        var counselor = await (await aurora.PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId), new AssignRoleRequest(a.Iara.MembershipId, RoleCode.Counselor, a.UnitX)))
            .Content.ReadFromJsonAsync<RoleAssignmentResponse>();
        using var chaplain = await aurora.PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId), new AssignRoleRequest(a.Iara.MembershipId, RoleCode.Chaplain, null));
        using var ended = await aurora.PostAsync(ApiRoutes.EndRoleAssignment(a.ClubId, counselor!.AssignmentId), null);
        var leaders = await aurora.GetFromJsonAsync<List<LeaderResponse>>(ApiRoutes.Leaders(a.ClubId));

        Assert.Equal(HttpStatusCode.OK, chaplain.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ended.StatusCode);
        var iara = Assert.Single(leaders!, l => l.MembershipId == a.Iara.MembershipId);
        Assert.Equal(new[] { RoleCode.Chaplain }, iara.Roles.Select(r => r.Role));
        Assert.Equal(2, leaders!.Count(l => l.Roles.Any(r => r.Role == RoleCode.Chaplain))); // a Iara e a "Outra Capelã": cargo não único
    }

    // a: retirar revoga o acesso no pedido seguinte (RN-ORG-006).
    [Fact]
    public async Task CA_ORG_003_Removal_revokes_access_on_next_request()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();
        using var caio = api.CreateClientAs(a.Caio);
        var caioSecretary = (await api.CreateClientAs(a.Aurora).GetFromJsonAsync<List<LeaderResponse>>(ApiRoutes.Leaders(a.ClubId)))!
            .Single(l => l.MembershipId == a.Caio.MembershipId).Roles.Single(r => r.Role == RoleCode.Secretary).AssignmentId;

        using var before = await caio.GetAsync(ApiRoutes.Leaders(a.ClubId));
        await api.CreateClientAs(a.Aurora).PostAsync(ApiRoutes.EndRoleAssignment(a.ClubId, caioSecretary), null);
        using var after = await caio.GetAsync(ApiRoutes.Leaders(a.ClubId));

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        await ProblemAssert.HasCodeAsync(after, HttpStatusCode.Forbidden, ProblemCodes.PermissionDenied);
    }

    // b: outros cargos; c: alvo e unidade de B; d: a si mesmo, diretor, alvo inativo, idade e sexo da unidade.
    [Fact]
    public async Task CA_ORG_003_Other_roles_scope_and_attributes_are_denied()
    {
        var a = await TestData.CreateClubAAsync(db);
        var b = await TestData.CreateClubBAsync(db);
        var suspended = await TestData.AddMemberAsync(db, a.ClubId, await TestData.CreatePersonAsync(db, "Suspensa"), MembershipStatus.Suspended);
        var male = await TestData.CreateMemberAsync(db, a.ClubId, "Rapaz", sex: Sex.Male);
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);
        using var caio = api.CreateClientAs(a.Caio);

        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(a.Dalva).PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId),
            new AssignRoleRequest(a.Iara.MembershipId, RoleCode.Chaplain, null)), HttpStatusCode.Forbidden, ProblemCodes.PermissionDenied);
        Assert.Equal(HttpStatusCode.NotFound, (await aurora.PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId),
            new AssignRoleRequest(b.Bento.MembershipId, RoleCode.Chaplain, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await aurora.PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId),
            new AssignRoleRequest(male.MembershipId, RoleCode.Counselor, b.UnitZ))).StatusCode);
        await ProblemAssert.HasCodeAsync(await caio.PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId),
            new AssignRoleRequest(a.Caio.MembershipId, RoleCode.Chaplain, null)), HttpStatusCode.Forbidden, ProblemCodes.SelfAction);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId),
            new AssignRoleRequest(a.Iara.MembershipId, RoleCode.Director, null)), HttpStatusCode.Forbidden, ProblemCodes.RoleDirectorForbidden);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId),
            new AssignRoleRequest(suspended.MembershipId, RoleCode.Chaplain, null)), HttpStatusCode.Forbidden, ProblemCodes.RoleTargetIneligible);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId),
            new AssignRoleRequest(male.MembershipId, RoleCode.Counselor, a.UnitX)), HttpStatusCode.Forbidden, ProblemCodes.RoleUnitSexMismatch);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId),
            new AssignRoleRequest(male.MembershipId, RoleCode.Counselor, null)), HttpStatusCode.BadRequest, ProblemCodes.RoleUnitRequired);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId),
            new AssignRoleRequest(male.MembershipId, RoleCode.Chaplain, a.UnitY)), HttpStatusCode.BadRequest, ProblemCodes.RoleUnitNotAllowed);
    }

    // CA-ORG-001: 17 anos não recebe tesoureiro; com 18 completos, recebe. Sem cadastro de menor no R1 (fixture fictícia).
    [Fact]
    public async Task CA_ORG_001_Treasurer_needs_18_years()
    {
        var club = await TestData.CreateClubAsync(db);
        var director = await TestData.CreateMemberAsync(db, club, "Diretora", role: RoleCode.Director);
        var secretary = await TestData.CreateMemberAsync(db, club, "Secretário", sex: Sex.Male, role: RoleCode.Secretary);
        var seventeen = await TestData.CreateMemberAsync(db, club, "Dezessete", age: 17);
        var eighteen = await TestData.CreateMemberAsync(db, club, "Dezoito", age: 18);
        await using var api = Api();
        using var caio = api.CreateClientAs(secretary);

        await ProblemAssert.HasCodeAsync(await caio.PostAsJsonAsync(ApiRoutes.RoleAssignments(club),
            new AssignRoleRequest(seventeen.MembershipId, RoleCode.Treasurer, null)), HttpStatusCode.Forbidden, ProblemCodes.AgeBelowMinimum);
        Assert.Equal(HttpStatusCode.OK, (await caio.PostAsJsonAsync(ApiRoutes.RoleAssignments(club),
            new AssignRoleRequest(eighteen.MembershipId, RoleCode.Treasurer, null))).StatusCode);
        Assert.NotNull(director);
    }

    // CA-ORG-002: o secretário não atribui a si mesmo diretor associado; a diretora pode atribuir a ele.
    [Fact]
    public async Task CA_ORG_002_Secretary_cannot_self_assign_but_director_can_assign()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();

        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(a.Caio).PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId),
            new AssignRoleRequest(a.Caio.MembershipId, RoleCode.AssociateDirector, null)), HttpStatusCode.Forbidden, ProblemCodes.SelfAction);
        Assert.Equal(HttpStatusCode.OK, (await api.CreateClientAs(a.Aurora).PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId),
            new AssignRoleRequest(a.Caio.MembershipId, RoleCode.AssociateDirector, null))).StatusCode);
    }

    // D-170: segunda tesouraria vigente é negada, inclusive em concorrência; o mesmo cargo duas vezes também.
    [Fact]
    public async Task CA_ORG_003_Unique_roles_hold_under_concurrency()
    {
        var club = await TestData.CreateClubAsync(db);
        var director = await TestData.CreateMemberAsync(db, club, "Diretora", role: RoleCode.Director);
        var candidates = new List<TestActor>();
        for (var i = 0; i < 4; i++)
            candidates.Add(await TestData.CreateMemberAsync(db, club, $"Candidata {i}"));
        await using var api = Api();
        using var client = api.CreateClientAs(director);

        var results = await Task.WhenAll(candidates.Select(c => client.PostAsJsonAsync(ApiRoutes.RoleAssignments(club), new AssignRoleRequest(c.MembershipId, RoleCode.Treasurer, null))));
        using var twice = await client.PostAsJsonAsync(ApiRoutes.RoleAssignments(club), new AssignRoleRequest(candidates[0].MembershipId, RoleCode.Chaplain, null));
        using var again = await client.PostAsJsonAsync(ApiRoutes.RoleAssignments(club), new AssignRoleRequest(candidates[0].MembershipId, RoleCode.Chaplain, null));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        foreach (var result in results.Where(r => r.StatusCode != HttpStatusCode.OK))
            await ProblemAssert.HasCodeAsync(result, HttpStatusCode.Conflict, ProblemCodes.RoleUniqueTaken);
        await ProblemAssert.HasCodeAsync(again, HttpStatusCode.Conflict, ProblemCodes.RoleAlreadyAssigned);
    }

    // D-161: a retirada corretiva dispensa a elegibilidade atual do alvo, mas nunca a si mesmo e nunca o diretor.
    [Fact]
    public async Task CA_ORG_003_Corrective_removal_skips_target_eligibility()
    {
        var a = await TestData.CreateClubAAsync(db);
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, "UPDATE persons SET sex = 'Male' WHERE id = @p", ("p", a.Lina.PersonId));
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);
        var leaders = await aurora.GetFromJsonAsync<List<LeaderResponse>>(ApiRoutes.Leaders(a.ClubId));
        var linaCounselor = leaders!.Single(l => l.MembershipId == a.Lina.MembershipId).Roles.Single().AssignmentId;
        var auroraDirector = leaders!.Single(l => l.MembershipId == a.Aurora.MembershipId).Roles.Single().AssignmentId;
        var caioSecretary = leaders!.Single(l => l.MembershipId == a.Caio.MembershipId).Roles.Single().AssignmentId;

        Assert.Equal(HttpStatusCode.NoContent, (await aurora.PostAsync(ApiRoutes.EndRoleAssignment(a.ClubId, linaCounselor), null)).StatusCode);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsync(ApiRoutes.EndRoleAssignment(a.ClubId, auroraDirector), null), HttpStatusCode.Forbidden, ProblemCodes.RoleDirectorForbidden);
        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(a.Caio).PostAsync(ApiRoutes.EndRoleAssignment(a.ClubId, caioSecretary), null),
            HttpStatusCode.Forbidden, ProblemCodes.SelfAction);
    }

    // CA-AUD-002: a auditoria da atribuição identifica ator, ação, instante e alvo só por IDs.
    [Fact]
    public async Task CA_AUD_002_Assignment_audit_has_only_ids()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();

        var created = await (await api.CreateClientAs(a.Caio).PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId),
            new AssignRoleRequest(a.Iara.MembershipId, RoleCode.Counselor, a.UnitX))).Content.ReadFromJsonAsync<RoleAssignmentResponse>();

        var row = await OwnerSql.ScalarAsync<string>(db.OwnerConnectionString, """
            SELECT concat_ws('|', actor_person_id, action, target_type, target_id, club_id, coalesce(reason_code, ''), array_to_string(fields, ','))
            FROM audit_entries WHERE target_id = @t AND action = 'RoleAssigned'
            """, ("t", created!.AssignmentId));
        Assert.Equal($"{a.Caio.PersonId}|RoleAssigned|RoleAssignment|{created.AssignmentId}|{a.ClubId}||", row);
    }
}
