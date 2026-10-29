using System.Net;
using System.Net.Http.Json;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace Dbvprovas.Api.Tests.Organization;

public sealed class DirectorNominationTests(PostgresFixture db)
{
    private ApiFactory Api(FakeTimeProvider? clock = null) => new(db, "Production") { UseTestAuthentication = true, Clock = clock };

    private static async Task<NominationResponse> NominateAsync(HttpClient director, Guid clubId, Guid nominee) =>
        (await (await director.PostAsJsonAsync(ApiRoutes.DirectorNominations(clubId), new NominateDirectorRequest(nominee))).Content
            .ReadFromJsonAsync<NominationResponse>())!;

    // CA-ORG-004 a: indicação pendente por até 7 dias, sem troca imediata; a diretora cancela a própria; o indicado é avisado.
    [Fact]
    public async Task CA_ORG_004_Director_nominates_and_cancels()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);

        var nomination = await NominateAsync(aurora, a.ClubId, a.Iara.MembershipId);
        var current = await api.CreateClientAs(a.Iara).GetFromJsonAsync<CurrentNominationResponse>(ApiRoutes.CurrentDirectorNomination(a.ClubId));
        using var cancelled = await aurora.PostAsync(ApiRoutes.CancelDirectorNomination(a.ClubId, nomination.Id), null);

        Assert.Equal(NominationStatus.Pending, nomination.Status);
        Assert.Equal(nomination.Id, current!.Nomination!.Id);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(1, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM notifications WHERE recipient_person_id = @p AND event = 'DirectorNominated' AND object_id = @n",
            ("p", a.Iara.PersonId), ("n", nomination.Id)));
        Assert.Equal(1, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM role_assignments WHERE club_id = @c AND role = 'Director' AND ended_at IS NULL AND membership_id = @m",
            ("c", a.ClubId), ("m", a.Aurora.MembershipId)));
    }

    // CA-ORG-004 b/c/d e D-163: não diretor, alvo de B, a si mesma, alvo menor ou inativo, segunda pendente e cancelar depois de decidida.
    [Fact]
    public async Task CA_ORG_004_Invalid_nominations_are_denied()
    {
        var a = await TestData.CreateClubAAsync(db);
        var b = await TestData.CreateClubBAsync(db);
        var minor = await TestData.CreateMemberAsync(db, a.ClubId, "Menor", age: 17);
        var suspended = await TestData.AddMemberAsync(db, a.ClubId, await TestData.CreatePersonAsync(db, "Suspensa"), MembershipStatus.Suspended);
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);

        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(a.Caio).PostAsJsonAsync(ApiRoutes.DirectorNominations(a.ClubId),
            new NominateDirectorRequest(a.Iara.MembershipId)), HttpStatusCode.Forbidden, ProblemCodes.PermissionDenied);
        Assert.Equal(HttpStatusCode.NotFound, (await aurora.PostAsJsonAsync(ApiRoutes.DirectorNominations(a.ClubId), new NominateDirectorRequest(b.Bento.MembershipId))).StatusCode);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.DirectorNominations(a.ClubId), new NominateDirectorRequest(a.Aurora.MembershipId)),
            HttpStatusCode.Forbidden, ProblemCodes.SelfAction);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.DirectorNominations(a.ClubId), new NominateDirectorRequest(minor.MembershipId)),
            HttpStatusCode.Forbidden, ProblemCodes.AgeBelowMinimum);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.DirectorNominations(a.ClubId), new NominateDirectorRequest(suspended.MembershipId)),
            HttpStatusCode.Forbidden, ProblemCodes.NominationTargetIneligible);
        var first = await NominateAsync(aurora, a.ClubId, a.Iara.MembershipId);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.DirectorNominations(a.ClubId), new NominateDirectorRequest(a.Caio.MembershipId)),
            HttpStatusCode.Conflict, ProblemCodes.NominationPendingExists);
        await api.CreateClientAs(a.Iara).PostAsync(ApiRoutes.DeclineDirectorNomination(a.ClubId, first.Id), null);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsync(ApiRoutes.CancelDirectorNomination(a.ClubId, first.Id), null),
            HttpStatusCode.Conflict, ProblemCodes.StateConflict);
    }

    // CA-ORG-005 a, D-014, D-158: o indicado aceita; troca atômica; a antiga diretora fica sem nenhum cargo; o novo deixa a secretaria
    // e mantém os outros cargos; admin e diretoria são avisados; repetir não troca de novo.
    [Fact]
    public async Task CA_ORG_005_Nominee_accepts_and_direction_changes_atomically()
    {
        var a = await TestData.CreateClubAAsync(db);
        await TestData.AssignRoleAsync(db, a.Aurora, RoleCode.Chaplain);
        await TestData.AssignRoleAsync(db, a.Caio, RoleCode.Pastor);
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = Api();
        var nomination = await NominateAsync(api.CreateClientAs(a.Aurora), a.ClubId, a.Caio.MembershipId);
        using var caio = api.CreateClientAs(a.Caio);

        using var accepted = await caio.PostAsync(ApiRoutes.AcceptDirectorNomination(a.ClubId, nomination.Id), null);
        using var again = await caio.PostAsync(ApiRoutes.AcceptDirectorNomination(a.ClubId, nomination.Id), null);
        var me = await caio.GetFromJsonAsync<MeResponse>(ApiRoutes.Me);

        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(new[] { RoleCode.Director, RoleCode.Pastor }.Order(), Assert.Single(me!.Clubs).Roles.Order());
        Assert.Equal(0, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM role_assignments WHERE membership_id = @m AND ended_at IS NULL", ("m", a.Aurora.MembershipId)));
        Assert.Equal(1, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM role_assignments WHERE club_id = @c AND role = 'Director' AND ended_at IS NULL", ("c", a.ClubId)));
        Assert.Equal(1, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM notifications WHERE recipient_person_id = @p AND event = 'DirectorChanged' AND club_id = @c",
            ("p", admin.PersonId), ("c", a.ClubId)));
    }

    // CA-ORG-005 a: recusar mantém a direção.
    [Fact]
    public async Task CA_ORG_005_Decline_keeps_current_direction()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();
        var nomination = await NominateAsync(api.CreateClientAs(a.Aurora), a.ClubId, a.Iara.MembershipId);

        using var declined = await api.CreateClientAs(a.Iara).PostAsync(ApiRoutes.DeclineDirectorNomination(a.ClubId, nomination.Id), null);

        Assert.Equal(HttpStatusCode.NoContent, declined.StatusCode);
        Assert.Equal(1, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM role_assignments WHERE membership_id = @m AND role = 'Director' AND ended_at IS NULL", ("m", a.Aurora.MembershipId)));
    }

    // CA-ORG-005 b/c: outra pessoa, inclusive a diretora ou o admin pela via do clube, não decide pelo indicado.
    [Fact]
    public async Task CA_ORG_005_Only_the_nominee_decides()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);
        var nomination = await NominateAsync(aurora, a.ClubId, a.Iara.MembershipId);

        await ProblemAssert.HasCodeAsync(await aurora.PostAsync(ApiRoutes.AcceptDirectorNomination(a.ClubId, nomination.Id), null),
            HttpStatusCode.Forbidden, ProblemCodes.PermissionDenied);
        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(a.Caio).PostAsync(ApiRoutes.DeclineDirectorNomination(a.ClubId, nomination.Id), null),
            HttpStatusCode.Forbidden, ProblemCodes.PermissionDenied);
        Assert.Equal(HttpStatusCode.NotFound,
            (await api.CreateClientAs(await TestData.CreateAdminAsync(db)).PostAsync(ApiRoutes.AcceptDirectorNomination(a.ClubId, nomination.Id), null)).StatusCode);
    }

    // CA-ORG-005 d, D-132, D-165: vencida no instante limite; indicante sem direção vigente; indicado sem participação.
    [Fact]
    public async Task CA_ORG_005_Expired_or_invalidated_nominations_cannot_be_accepted()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var a = await TestData.CreateClubAAsync(db, clock);
        await using var api = Api(clock);
        var nomination = await NominateAsync(api.CreateClientAs(a.Aurora), a.ClubId, a.Iara.MembershipId);

        clock.Advance(TimeSpan.FromDays(7));
        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(a.Iara).PostAsync(ApiRoutes.AcceptDirectorNomination(a.ClubId, nomination.Id), null),
            HttpStatusCode.Conflict, ProblemCodes.StateConflict);

        var second = await NominateAsync(api.CreateClientAs(a.Aurora), a.ClubId, a.Dalva.MembershipId);
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString,
            "UPDATE memberships SET status = 'Suspended', suspended_at = now() WHERE id = @m", ("m", a.Dalva.MembershipId));
        Assert.Equal(HttpStatusCode.NotFound, (await api.CreateClientAs(a.Dalva).PostAsync(ApiRoutes.AcceptDirectorNomination(a.ClubId, second.Id), null)).StatusCode);
    }

    // CA-ORG-005 d, D-163: aceitar contra cancelar ao mesmo tempo preserva a primeira transição válida, sem dois diretores.
    [Fact]
    public async Task CA_ORG_005_Accept_versus_cancel_keeps_one_director()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);
        var nomination = await NominateAsync(aurora, a.ClubId, a.Iara.MembershipId);

        var results = await Task.WhenAll(
            api.CreateClientAs(a.Iara).PostAsync(ApiRoutes.AcceptDirectorNomination(a.ClubId, nomination.Id), null),
            aurora.PostAsync(ApiRoutes.CancelDirectorNomination(a.ClubId, nomination.Id), null));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Equal(1, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM role_assignments WHERE club_id = @c AND role = 'Director' AND ended_at IS NULL", ("c", a.ClubId)));
        foreach (var result in results)
            result.Dispose();
    }

    // CA-ORG-004, CA-ORG-005: membro que não é diretor nem indicado recebe a mesma resposta com e sem indicação.
    [Fact]
    public async Task CA_ORG_004_Ordinary_member_sees_the_same_current_nomination_with_or_without_one()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();
        using var iara = api.CreateClientAs(a.Iara);

        using var without = await iara.GetAsync(ApiRoutes.CurrentDirectorNomination(a.ClubId));
        await NominateAsync(api.CreateClientAs(a.Aurora), a.ClubId, a.Caio.MembershipId);
        using var with = await iara.GetAsync(ApiRoutes.CurrentDirectorNomination(a.ClubId));

        Assert.Equal(HttpStatusCode.OK, without.StatusCode);
        Assert.Equal(HttpStatusCode.OK, with.StatusCode);
        Assert.Equal(await without.Content.ReadAsStringAsync(), await with.Content.ReadAsStringAsync());
    }

    // D-158: se a direção muda, a indicação posterior é negada e não deixa pendência.
    [Fact]
    public async Task CA_ORG_004_Nomination_after_director_replaced_is_denied_and_leaves_no_pending()
    {
        await using var api = Api();
        var sequential = await TestData.CreateClubAAsync(db);
        var sequentialAdmin = await TestData.CreateAdminAsync(db);
        using var sequentialReplace = await api.CreateClientAs(sequentialAdmin).PostAsJsonAsync(ApiRoutes.AdminReplaceDirector(sequential.ClubId),
            new ReplaceDirectorRequest(sequential.Dalva.MembershipId, ReasonCode.DirectorUnavailable));
        using var sequentialNominate = await api.CreateClientAs(sequential.Aurora).PostAsJsonAsync(ApiRoutes.DirectorNominations(sequential.ClubId),
            new NominateDirectorRequest(sequential.Iara.MembershipId));
        Assert.Equal(HttpStatusCode.NoContent, sequentialReplace.StatusCode);
        await ProblemAssert.HasCodeAsync(sequentialNominate, HttpStatusCode.Forbidden, ProblemCodes.PermissionDenied);
        Assert.Equal(0, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM director_nominations WHERE club_id = @c AND status = 'Pending'", ("c", sequential.ClubId)));

        for (var round = 0; round < 6; round++)
        {
            var a = await TestData.CreateClubAAsync(db);
            var admin = await TestData.CreateAdminAsync(db);
            using var aurora = api.CreateClientAs(a.Aurora);
            using var adminClient = api.CreateClientAs(admin);

            var results = await Task.WhenAll(
                adminClient.PostAsJsonAsync(ApiRoutes.AdminReplaceDirector(a.ClubId),
                    new ReplaceDirectorRequest(a.Dalva.MembershipId, ReasonCode.DirectorUnavailable)),
                aurora.PostAsJsonAsync(ApiRoutes.DirectorNominations(a.ClubId), new NominateDirectorRequest(a.Iara.MembershipId)));

            Assert.All(results, r => Assert.True(
                r.IsSuccessStatusCode || r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Conflict, r.StatusCode.ToString()));
            Assert.Equal(0, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
                "SELECT count(*) FROM director_nominations WHERE club_id = @c AND status = 'Pending'", ("c", a.ClubId)));
            foreach (var result in results)
                result.Dispose();
        }
    }
}
