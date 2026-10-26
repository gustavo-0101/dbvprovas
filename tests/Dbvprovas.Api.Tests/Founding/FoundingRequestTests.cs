using System.Net;
using System.Net.Http.Json;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Founding;

public sealed class FoundingRequestTests(PostgresFixture db)
{
    private static CreateFoundingRequest Body(bool exception = false, string name = "Clube Novo", string city = "Cidade Nova", string? church = null,
        string version = ConsentTerms.AdultOwnParticipationVersion, bool consent = true) =>
        new(name, city, church, exception, version, consent);

    private ApiFactory Api() => new(db, "Production") { UseTestAuthentication = true };

    private Task<long> CountAsync(string sql, params (string, object)[] parameters) => OwnerSql.ScalarAsync<long>(db.OwnerConnectionString, sql, parameters);

    private async Task MarkFoundedBeforeAsync(TestPerson person)
    {
        var club = await TestData.CreateClubAsync(db);
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString,
            "INSERT INTO founding_records (id, person_id, club_id, approved_at) VALUES (@id, @p, @c, now())",
            ("id", Guid.CreateVersion7()), ("p", person.PersonId), ("c", club));
    }

    // a: adulto elegível envia, vê o pendente, cancela; nenhum clube nasce; consentimento pré-fundação registrado e encerrado (CA-PRV-008).
    [Fact]
    public async Task CA_TEN_009_Adult_sends_founding_request_and_cancels_it()
    {
        var person = await TestData.CreatePersonAsync(db, "Fundadora");
        await using var api = Api();
        using var client = api.CreateClientAs(person);

        using var sent = await client.PostAsJsonAsync(ApiRoutes.FoundingRequests, Body());
        var request = await sent.Content.ReadFromJsonAsync<FoundingRequestResponse>();
        using var cancelled = await client.PostAsync(ApiRoutes.CancelMyFoundingRequest(request!.Id), null);
        var mine = await client.GetFromJsonAsync<List<FoundingRequestResponse>>(ApiRoutes.MyFoundingRequests);

        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        Assert.Equal(FoundingRequestStatus.Pending, request.Status);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(FoundingRequestStatus.Cancelled, Assert.Single(mine!).Status);
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM clubs c JOIN founding_requests f ON f.proposed_club_id = c.id WHERE f.id = @id", ("id", request.Id)));
        Assert.Equal(1, await CountAsync("""
            SELECT count(*) FROM consents WHERE founding_request_id = @id AND club_id IS NULL AND person_id = @p
                AND term_version = @v AND ended_at IS NOT NULL AND end_cause = 'RequestClosed'
            """, ("id", request.Id), ("p", person.PersonId), ("v", ConsentTerms.AdultOwnParticipationVersion)));
        Assert.Equal(3, await CountAsync("""
            SELECT count(*) FROM audit_entries WHERE actor_person_id = @p AND scope = 'Platform'
                AND action IN ('FoundingRequested', 'ConsentGranted', 'FoundingCancelled')
            """, ("p", person.PersonId)));
    }

    // a: repetir o envio não duplica; dois envios ao mesmo tempo criam um só pedido (D-140, RN-JOIN-006).
    [Fact]
    public async Task CA_TEN_009_Repeated_or_concurrent_sends_create_one_request()
    {
        var person = await TestData.CreatePersonAsync(db, "Repetida");
        await using var api = Api();
        using var client = api.CreateClientAs(person);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsJsonAsync(ApiRoutes.FoundingRequests, Body())));
        using var again = await client.PostAsJsonAsync(ApiRoutes.FoundingRequests, Body());

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        await ProblemAssert.HasCodeAsync(again, HttpStatusCode.Conflict, ProblemCodes.PendingRequestExists);
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM founding_requests WHERE requester_person_id = @p", ("p", person.PersonId)));
        foreach (var response in responses)
            response.Dispose();
    }

    // a: quem já fundou pode pedir pedindo exceção (D-157).
    [Fact]
    public async Task CA_TEN_009_Founder_asking_for_exception_can_send()
    {
        var person = await TestData.CreatePersonAsync(db, "Já Fundou");
        await MarkFoundedBeforeAsync(person);
        await using var api = Api();

        using var sent = await api.CreateClientAs(person).PostAsJsonAsync(ApiRoutes.FoundingRequests, Body(exception: true));

        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        Assert.True((await sent.Content.ReadFromJsonAsync<FoundingRequestResponse>())!.ExceptionRequested);
    }

    // b: sem sessão, o envio é negado.
    [Fact]
    public async Task CA_TEN_009_Without_session_send_is_denied()
    {
        await using var api = Api();

        using var sent = await api.CreateClient().PostAsJsonAsync(ApiRoutes.FoundingRequests, Body());

        Assert.Equal(HttpStatusCode.Unauthorized, sent.StatusCode);
    }

    // c: ninguém cancela nem vê o pedido de outro titular.
    [Fact]
    public async Task CA_TEN_009_Other_titular_cannot_cancel_or_list()
    {
        var owner = await TestData.CreatePersonAsync(db, "Dona");
        var other = await TestData.CreatePersonAsync(db, "Outra");
        await using var api = Api();
        using var ownerClient = api.CreateClientAs(owner);
        using var otherClient = api.CreateClientAs(other);
        var request = await (await ownerClient.PostAsJsonAsync(ApiRoutes.FoundingRequests, Body())).Content.ReadFromJsonAsync<FoundingRequestResponse>();

        using var cancel = await otherClient.PostAsync(ApiRoutes.CancelMyFoundingRequest(request!.Id), null);
        var otherList = await otherClient.GetFromJsonAsync<List<FoundingRequestResponse>>(ApiRoutes.MyFoundingRequests);

        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Empty(otherList!);
    }

    // d: menor, diretor atual, pendência de entrada e já fundador sem exceção são negados; a exceção não dispensa os outros.
    [Fact]
    public async Task CA_TEN_009_Ineligible_requesters_are_denied()
    {
        var minor = await TestData.CreatePersonAsync(db, "Menor", age: 17);
        var club = await TestData.CreateClubAsync(db);
        var director = await TestData.CreateMemberAsync(db, club, "Diretora Atual", role: RoleCode.Director);
        var founder = await TestData.CreatePersonAsync(db, "Fundadora Antiga");
        await MarkFoundedBeforeAsync(founder);
        var otherClub = await TestData.CreateClubAsync(db);
        var founderDirector = await TestData.CreateMemberAsync(db, otherClub, "Fundadora Diretora", role: RoleCode.Director);
        await MarkFoundedBeforeAsync(new TestPerson(founderDirector.PersonId, founderDirector.AccountId, founderDirector.Name));
        var pending = await TestData.CreatePersonAsync(db, "Com Pedido");
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
            INSERT INTO consents (id, person_id, kind, club_id, term_version, granted_at) VALUES (@cid, @p, 'OwnParticipation', @c, 'v', now());
            INSERT INTO join_requests (id, club_id, candidate_person_id, created_by_account_id, kind, path, status, consent_id, queued_at, expires_at)
                VALUES (@id, @c, @p, @a, 'Leader', 'Search', 'Queued', @cid, now(), now() + interval '14 days');
            """, ("cid", Guid.CreateVersion7()), ("p", pending.PersonId), ("c", club), ("id", Guid.CreateVersion7()), ("a", pending.AccountId));
        await using var api = Api();

        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(minor).PostAsJsonAsync(ApiRoutes.FoundingRequests, Body()),
            HttpStatusCode.Forbidden, ProblemCodes.AgeBelowMinimum);
        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(director).PostAsJsonAsync(ApiRoutes.FoundingRequests, Body()),
            HttpStatusCode.Forbidden, ProblemCodes.FoundingIsDirector);
        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(founder).PostAsJsonAsync(ApiRoutes.FoundingRequests, Body()),
            HttpStatusCode.Forbidden, ProblemCodes.FoundingAlreadyFounder);
        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(founderDirector).PostAsJsonAsync(ApiRoutes.FoundingRequests, Body(exception: true)),
            HttpStatusCode.Forbidden, ProblemCodes.FoundingIsDirector);
        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(pending).PostAsJsonAsync(ApiRoutes.FoundingRequests, Body()),
            HttpStatusCode.Conflict, ProblemCodes.PendingRequestExists);
    }

    // d: recusa anterior não conta como fundação (RN-TEN-003).
    [Fact]
    public async Task CA_TEN_009_Rejected_request_does_not_count_as_founding()
    {
        var person = await TestData.CreatePersonAsync(db, "Recusada Antes");
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
            INSERT INTO founding_requests (id, requester_person_id, requester_account_id, club_name, club_city, proposed_club_id,
                    status, exception_requested, exception_granted, reason_code, created_at, closed_at)
                VALUES (@id, @p, @a, 'Antigo', 'Cidade', @c, 'Rejected', false, false, 'ConditionsNotMet', now(), now())
            """, ("id", Guid.CreateVersion7()), ("p", person.PersonId), ("a", person.AccountId), ("c", Guid.CreateVersion7()));
        await using var api = Api();

        using var sent = await api.CreateClientAs(person).PostAsJsonAsync(ApiRoutes.FoundingRequests, Body());

        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
    }

    // CA-ID-004: cadastro incompleto não conclui o ingresso; CA-PRV-008: sem aceite ou com versão velha, nada é criado.
    [Fact]
    public async Task CA_TEN_009_Incomplete_profile_or_missing_consent_is_denied()
    {
        var incomplete = await TestData.CreatePersonAsync(db, "Incompleta", age: null);
        var person = await TestData.CreatePersonAsync(db, "Sem Aceite");
        await using var api = Api();
        using var client = api.CreateClientAs(person);

        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(incomplete).PostAsJsonAsync(ApiRoutes.FoundingRequests, Body()),
            HttpStatusCode.Forbidden, ProblemCodes.ProfileIncomplete);
        await ProblemAssert.HasCodeAsync(await client.PostAsJsonAsync(ApiRoutes.FoundingRequests, Body(consent: false)),
            HttpStatusCode.BadRequest, ProblemCodes.ConsentRequired);
        await ProblemAssert.HasCodeAsync(await client.PostAsJsonAsync(ApiRoutes.FoundingRequests, Body(version: "versao-velha")),
            HttpStatusCode.BadRequest, ProblemCodes.ConsentOutdated);
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM founding_requests WHERE requester_person_id = @p", ("p", person.PersonId)));
    }

    // RF-TEN-005: texto vazio, enorme ou com caractere de controle é recusado; espaços das pontas são cortados.
    [Fact]
    public async Task Founding_request_rejects_blank_huge_or_control_text()
    {
        var person = await TestData.CreatePersonAsync(db, "Texto");
        await using var api = Api();
        using var client = api.CreateClientAs(person);

        foreach (var body in new[] { Body(name: "   "), Body(city: new string('x', 121)), Body(name: "Clube\u0007Novo"), Body(church: new string('y', 121)) })
            await ProblemAssert.HasCodeAsync(await client.PostAsJsonAsync(ApiRoutes.FoundingRequests, body), HttpStatusCode.BadRequest, ProblemCodes.ValidationText);
        var ok = await (await client.PostAsJsonAsync(ApiRoutes.FoundingRequests, Body(name: "  Clube Aparado  ", church: "  "))).Content
            .ReadFromJsonAsync<FoundingRequestResponse>();

        Assert.Equal("Clube Aparado", ok!.ClubName);
        Assert.Null(ok.Church);
    }
}
