using System.Net;
using System.Net.Http.Json;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Api.Tests.Organization;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Founding;

public sealed class FoundingDecisionTests(PostgresFixture db)
{
    private ApiFactory Api() => new(db, "Production") { UseTestAuthentication = true };

    private Task<long> CountAsync(string sql, params (string, object)[] parameters) => OwnerSql.ScalarAsync<long>(db.OwnerConnectionString, sql, parameters);

    private static CreateFoundingRequest Body(bool exception = false) =>
        new("Clube Novo", "Cidade Nova", null, exception, ConsentTerms.AdultOwnParticipationVersion, true);

    private static async Task<Guid> SendAsync(ApiFactory api, TestPerson person, bool exception = false) =>
        (await (await api.CreateClientAs(person).PostAsJsonAsync(ApiRoutes.FoundingRequests, Body(exception))).Content
            .ReadFromJsonAsync<FoundingRequestResponse>())!.Id;

    // a: aprovar cria clube, participação e diretor uma vez; associa o consentimento ao clube; avisa e enfileira o e-mail.
    [Fact]
    public async Task CA_TEN_010_Admin_approves_and_creates_club_membership_and_director_once()
    {
        var requester = await TestData.CreatePersonAsync(db, "Solicitante");
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = Api();
        var requestId = await SendAsync(api, requester);
        using var adminClient = api.CreateClientAs(admin);

        using var approved = await adminClient.PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(requestId), new ApproveFoundingRequest(false, null));
        var clubId = (await approved.Content.ReadFromJsonAsync<FoundingApprovedResponse>())!.ClubId;
        using var again = await adminClient.PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(requestId), new ApproveFoundingRequest(false, null));
        var me = await api.CreateClientAs(requester).GetFromJsonAsync<MeResponse>(ApiRoutes.Me);

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM clubs WHERE id = @c", ("c", clubId)));
        Assert.Equal(new[] { RoleCode.Director }, Assert.Single(me!.Clubs).Roles);
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM founding_records WHERE person_id = @p AND club_id = @c", ("p", requester.PersonId), ("c", clubId)));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM consents WHERE founding_request_id = @r AND club_id = @c AND ended_at IS NULL", ("r", requestId), ("c", clubId)));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM notifications WHERE recipient_person_id = @p AND event = 'FoundingApproved' AND object_id = @r", ("p", requester.PersonId), ("r", requestId)));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM email_outbox WHERE recipient_account_id = @a AND template = 'FoundingApproved' AND object_id = @r", ("a", requester.AccountId), ("r", requestId)));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM audit_entries WHERE action = 'FoundingApproved' AND target_id = @r AND scope = 'Platform' AND club_id = @c", ("r", requestId), ("c", clubId)));
    }

    // a, D-137: a participação anterior do fundador se encerra, com os cargos dela, e a auditoria fica no clube antigo.
    [Fact]
    public async Task CA_TEN_010_Approval_ends_founder_previous_membership()
    {
        var a = await TestData.CreateClubAAsync(db);
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = Api();
        var founder = new TestPerson(a.Lina.PersonId, a.Lina.AccountId, a.Lina.Name);
        var requestId = await SendAsync(api, founder);

        using var approved = await api.CreateClientAs(admin).PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(requestId), new ApproveFoundingRequest(false, null));

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("Ended", await OwnerSql.ScalarAsync<string>(db.OwnerConnectionString, "SELECT status FROM memberships WHERE id = @m", ("m", a.Lina.MembershipId)));
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM role_assignments WHERE membership_id = @m AND ended_at IS NULL", ("m", a.Lina.MembershipId)));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM audit_entries WHERE club_id = @c AND action = 'MembershipEnded' AND cause = 'FoundedNewClub'", ("c", a.ClubId)));
    }

    // a, RN-TEN-004: participação suspensa em outro clube também se encerra na aprovação; a pessoa fica só no clube novo.
    [Fact]
    public async Task CA_TEN_010_Approval_ends_a_suspended_previous_membership_too()
    {
        var a = await TestData.CreateClubAAsync(db);
        var admin = await TestData.CreateAdminAsync(db);
        var person = await TestData.CreatePersonAsync(db, "Suspensa Fundadora");
        var suspended = await TestData.AddMemberAsync(db, a.ClubId, person, MembershipStatus.Suspended);
        await using var api = Api();
        var requestId = await SendAsync(api, person);

        using var approved = await api.CreateClientAs(admin).PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(requestId), new ApproveFoundingRequest(false, null));

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM memberships WHERE id = @m AND status = 'Ended' AND status_cause = 'FoundedNewClub' AND suspended_at IS NULL",
            ("m", suspended.MembershipId)));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM memberships WHERE person_id = @p AND status <> 'Ended'", ("p", person.PersonId)));
    }

    // a: recusa com motivo encerra o consentimento pré-fundação, avisa e enfileira o e-mail; nenhum outro e-mail.
    [Fact]
    public async Task CA_TEN_010_Admin_rejects_with_reason()
    {
        var requester = await TestData.CreatePersonAsync(db, "Recusada");
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = Api();
        var requestId = await SendAsync(api, requester);

        using var rejected = await api.CreateClientAs(admin).PostAsJsonAsync(ApiRoutes.RejectFoundingRequest(requestId), new RejectFoundingRequest(ReasonCode.IncompleteInformation));

        Assert.Equal(HttpStatusCode.NoContent, rejected.StatusCode);
        Assert.Equal("IncompleteInformation", await OwnerSql.ScalarAsync<string>(db.OwnerConnectionString, "SELECT reason_code FROM founding_requests WHERE id = @r", ("r", requestId)));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM consents WHERE founding_request_id = @r AND ended_at IS NOT NULL", ("r", requestId)));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM email_outbox WHERE object_id = @r AND template = 'FoundingRejected'", ("r", requestId)));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM audit_entries WHERE action = 'FoundingRejected' AND reason_code = 'IncompleteInformation' AND target_id = @r", ("r", requestId)));
    }

    // b: quem não é admin não decide.
    [Fact]
    public async Task CA_TEN_010_Non_admin_cannot_decide()
    {
        var requester = await TestData.CreatePersonAsync(db, "Solicitante B");
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();
        var requestId = await SendAsync(api, requester);

        using var approve = await api.CreateClientAs(a.Aurora).PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(requestId), new ApproveFoundingRequest(false, null));
        using var list = await api.CreateClientAs(a.Aurora).GetAsync(ApiRoutes.AdminFoundingRequests);

        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    // c: o admin opera só no pedido escolhido e não ganha acesso aos dados do clube criado.
    [Fact]
    public async Task CA_TEN_010_Admin_operates_only_on_chosen_request()
    {
        var first = await TestData.CreatePersonAsync(db, "Primeira");
        var second = await TestData.CreatePersonAsync(db, "Segunda");
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = Api();
        var firstId = await SendAsync(api, first);
        var secondId = await SendAsync(api, second);
        using var adminClient = api.CreateClientAs(admin);

        var clubId = (await (await adminClient.PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(firstId), new ApproveFoundingRequest(false, null)))
            .Content.ReadFromJsonAsync<FoundingApprovedResponse>())!.ClubId;
        using var members = await adminClient.GetAsync(ApiRoutes.Members(clubId));

        Assert.Equal("Pending", await OwnerSql.ScalarAsync<string>(db.OwnerConnectionString, "SELECT status FROM founding_requests WHERE id = @r", ("r", secondId)));
        Assert.Equal(HttpStatusCode.NotFound, members.StatusCode);
    }

    // d: próprio pedido, recusa ou exceção sem motivo e pedido já decidido são negados.
    [Fact]
    public async Task CA_TEN_010_Own_request_missing_reason_and_terminal_are_denied()
    {
        var admin = await TestData.CreateAdminAsync(db);
        var requester = await TestData.CreatePersonAsync(db, "Outra");
        await using var api = Api();
        var ownId = await SendAsync(api, admin);
        var otherId = await SendAsync(api, requester);
        using var adminClient = api.CreateClientAs(admin);

        await ProblemAssert.HasCodeAsync(await adminClient.PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(ownId), new ApproveFoundingRequest(false, null)),
            HttpStatusCode.Forbidden, ProblemCodes.FoundingOwnRequest);
        await ProblemAssert.HasCodeAsync(await adminClient.PostAsJsonAsync(ApiRoutes.RejectFoundingRequest(otherId), new RejectFoundingRequest(null)),
            HttpStatusCode.BadRequest, ProblemCodes.ReasonRequired);
        await ProblemAssert.HasCodeAsync(await adminClient.PostAsJsonAsync(ApiRoutes.RejectFoundingRequest(otherId), new RejectFoundingRequest(ReasonCode.TemporaryLeave)),
            HttpStatusCode.BadRequest, ProblemCodes.ReasonRequired);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsJsonAsync(ApiRoutes.RejectFoundingRequest(otherId), new RejectFoundingRequest(ReasonCode.DuplicateRequest))).StatusCode);
        await ProblemAssert.HasCodeAsync(await adminClient.PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(otherId), new ApproveFoundingRequest(false, null)),
            HttpStatusCode.Conflict, ProblemCodes.StateConflict);
    }

    // d, D-157: pedido de quem já fundou só sai com a exceção concedida e motivada; a exceção não vale para quem nunca fundou.
    [Fact]
    public async Task CA_TEN_010_Founder_request_requires_granted_exception()
    {
        var founder = await TestData.CreatePersonAsync(db, "Fundadora");
        var club = await TestData.CreateClubAsync(db);
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString,
            "INSERT INTO founding_records (id, person_id, club_id, approved_at) VALUES (@id, @p, @c, now())",
            ("id", Guid.CreateVersion7()), ("p", founder.PersonId), ("c", club));
        var newcomer = await TestData.CreatePersonAsync(db, "Novata");
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = Api();
        var founderRequest = await SendAsync(api, founder, exception: true);
        var newcomerRequest = await SendAsync(api, newcomer);
        using var adminClient = api.CreateClientAs(admin);

        await ProblemAssert.HasCodeAsync(await adminClient.PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(founderRequest), new ApproveFoundingRequest(false, null)),
            HttpStatusCode.Forbidden, ProblemCodes.FoundingExceptionRequired);
        await ProblemAssert.HasCodeAsync(await adminClient.PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(founderRequest), new ApproveFoundingRequest(true, null)),
            HttpStatusCode.BadRequest, ProblemCodes.ReasonRequired);
        await ProblemAssert.HasCodeAsync(await adminClient.PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(newcomerRequest), new ApproveFoundingRequest(true, ReasonCode.NewFoundingAfterPrevious)),
            HttpStatusCode.BadRequest, ProblemCodes.FoundingExceptionNotApplicable);
        using var approved = await adminClient.PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(founderRequest), new ApproveFoundingRequest(true, ReasonCode.NewFoundingAfterPrevious));

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM audit_entries WHERE target_id = @r AND action = 'FoundingApproved' AND reason_code = 'NewFoundingAfterPrevious'", ("r", founderRequest)));
    }

    // d: condições revalidadas na decisão — quem virou diretor enquanto esperava é negado.
    [Fact]
    public async Task CA_TEN_010_Requester_who_became_director_meanwhile_is_denied()
    {
        var requester = await TestData.CreatePersonAsync(db, "Virou Diretora");
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = Api();
        var requestId = await SendAsync(api, requester);
        var club = await TestData.CreateClubAsync(db);
        var member = await TestData.AddMemberAsync(db, club, requester);
        await TestData.AssignRoleAsync(db, member, RoleCode.Director);

        using var approved = await api.CreateClientAs(admin).PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(requestId), new ApproveFoundingRequest(false, null));

        await ProblemAssert.HasCodeAsync(approved, HttpStatusCode.Forbidden, ProblemCodes.FoundingIsDirector);
    }

    // d, D-140: aprovar e recusar ao mesmo tempo aplicam só a primeira, sem clube pela metade.
    [Fact]
    public async Task CA_TEN_010_Concurrent_decisions_apply_only_the_first()
    {
        var requester = await TestData.CreatePersonAsync(db, "Disputada");
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = Api();
        var requestId = await SendAsync(api, requester);
        using var adminClient = api.CreateClientAs(admin);

        var results = await Task.WhenAll(
            adminClient.PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(requestId), new ApproveFoundingRequest(false, null)),
            adminClient.PostAsJsonAsync(ApiRoutes.RejectFoundingRequest(requestId), new RejectFoundingRequest(ReasonCode.DuplicateRequest)),
            adminClient.PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(requestId), new ApproveFoundingRequest(false, null)));

        Assert.Single(results, r => r.IsSuccessStatusCode);
        Assert.All(results.Where(r => !r.IsSuccessStatusCode), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        var status = await OwnerSql.ScalarAsync<string>(db.OwnerConnectionString, "SELECT status FROM founding_requests WHERE id = @r", ("r", requestId));
        var clubs = await CountAsync("SELECT count(*) FROM clubs c JOIN founding_requests f ON f.proposed_club_id = c.id WHERE f.id = @r", ("r", requestId));
        Assert.Equal(status == "Approved" ? 1 : 0, clubs);
        foreach (var result in results)
            result.Dispose();
    }

    // CA-AUD-008: o admin vê só o mínimo do solicitante, e cada leitura é auditada; CA-AUD-010: falha na auditoria não deixa decisão pela metade.
    [Fact]
    public async Task CA_AUD_008_Admin_sees_requester_minimum_and_each_read_is_audited()
    {
        var requester = await TestData.CreatePersonAsync(db, "Mínima");
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = Api();
        var requestId = await SendAsync(api, requester);
        using var adminClient = api.CreateClientAs(admin);

        var first = await adminClient.GetFromJsonAsync<List<AdminFoundingRequestResponse>>($"{ApiRoutes.AdminFoundingRequests}?limit=100");
        await adminClient.GetFromJsonAsync<List<AdminFoundingRequestResponse>>($"{ApiRoutes.AdminFoundingRequests}?limit=100");
        var raw = await adminClient.GetStringAsync($"{ApiRoutes.AdminFoundingRequests}?limit=100");

        var item = Assert.Single(first!, r => r.Id == requestId);
        Assert.Equal("Mínima", item.RequesterName);
        Assert.True(item.RequesterIsAdult);
        Assert.DoesNotContain("example.com", raw);
        Assert.DoesNotContain("birthDate", raw);
        Assert.True(await CountAsync("SELECT count(*) FROM audit_entries WHERE action = 'AdminPersonalDataRead' AND target_id = @p AND actor_person_id = @a",
            ("p", requester.PersonId), ("a", admin.PersonId)) >= 3);
    }

    [Fact]
    public async Task CA_AUD_010_Failed_audit_leaves_no_half_decision()
    {
        var requester = await TestData.CreatePersonAsync(db, "Atômica");
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = Api();
        var requestId = await SendAsync(api, requester);
        await using var faulty = new ApiFactory(db, "Production") { UseTestAuthentication = true };
        faulty.ExtraInterceptors.Add(new FailingInsertInterceptor("audit_entries"));

        using var approved = await faulty.CreateClientAs(admin).PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(requestId), new ApproveFoundingRequest(false, null));

        Assert.Equal(HttpStatusCode.InternalServerError, approved.StatusCode);
        Assert.Equal("Pending", await OwnerSql.ScalarAsync<string>(db.OwnerConnectionString, "SELECT status FROM founding_requests WHERE id = @r", ("r", requestId)));
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM clubs c JOIN founding_requests f ON f.proposed_club_id = c.id WHERE f.id = @r", ("r", requestId)));
    }

    // Lista que cresce: do mais antigo ao mais novo, ID como desempate, 1 a 100 por página (padrão 50).
    [Fact]
    public async Task CA_TEN_010_Pending_queue_is_stable_and_bounded()
    {
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = Api();
        using var adminClient = api.CreateClientAs(admin);
        for (var i = 0; i < 3; i++)
            await SendAsync(api, await TestData.CreatePersonAsync(db, $"Fila {i}"));

        var page = await adminClient.GetFromJsonAsync<List<AdminFoundingRequestResponse>>($"{ApiRoutes.AdminFoundingRequests}?limit=2");
        var defaults = await adminClient.GetFromJsonAsync<List<AdminFoundingRequestResponse>>(ApiRoutes.AdminFoundingRequests);

        Assert.Equal(2, page!.Count);
        Assert.InRange(defaults!.Count, 3, 50);
        Assert.Equal(page.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).Select(r => r.Id), page.Select(r => r.Id));
        Assert.Equal(defaults.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).Select(r => r.Id), defaults.Select(r => r.Id));
        await ProblemAssert.HasCodeAsync(await adminClient.GetAsync($"{ApiRoutes.AdminFoundingRequests}?limit=0"),
            HttpStatusCode.BadRequest, ProblemCodes.ValidationText);
        await ProblemAssert.HasCodeAsync(await adminClient.GetAsync($"{ApiRoutes.AdminFoundingRequests}?limit=101"),
            HttpStatusCode.BadRequest, ProblemCodes.ValidationText);
    }
}
