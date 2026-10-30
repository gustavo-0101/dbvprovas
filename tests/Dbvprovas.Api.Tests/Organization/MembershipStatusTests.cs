using System.Net;
using System.Net.Http.Json;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Organization;

public sealed class MembershipStatusTests(PostgresFixture db)
{
    private ApiFactory Api() => new(db, "Production") { UseTestAuthentication = true };

    private Task<string> StatusAsync(Guid membershipId) =>
        OwnerSql.ScalarAsync<string>(db.OwnerConnectionString, "SELECT status FROM memberships WHERE id = @m", ("m", membershipId));

    // CA-JOIN-006 a, D-155: suspender bloqueia o acesso, encerra todos os cargos e libera a vaga única; remover encerra sem apagar.
    [Fact]
    public async Task CA_JOIN_006_Suspend_ends_roles_and_remove_ends_membership()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);
        using var dalva = api.CreateClientAs(a.Dalva);

        using var suspended = await aurora.PostAsJsonAsync(ApiRoutes.SuspendMember(a.ClubId, a.Dalva.MembershipId), new MemberStatusRequest(ReasonCode.TemporaryLeave));
        using var dalvaAfter = await dalva.GetAsync(ApiRoutes.Club(a.ClubId));
        var newTreasurer = await TestData.CreateMemberAsync(db, a.ClubId, "Nova Tesoureira");
        using var reassigned = await aurora.PostAsJsonAsync(ApiRoutes.RoleAssignments(a.ClubId), new AssignRoleRequest(newTreasurer.MembershipId, RoleCode.Treasurer, null));
        using var removed = await aurora.PostAsJsonAsync(ApiRoutes.RemoveMember(a.ClubId, a.Iara.MembershipId), new MemberStatusRequest(ReasonCode.AdministrativeClosure));

        Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, dalvaAfter.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reassigned.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal("Suspended", await StatusAsync(a.Dalva.MembershipId));
        Assert.Equal("Ended", await StatusAsync(a.Iara.MembershipId));
        Assert.Equal(1, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM audit_entries WHERE target_id = @m AND action = 'MembershipSuspended' AND reason_code = 'TemporaryLeave'", ("m", a.Dalva.MembershipId)));
        Assert.Equal(0, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM notifications WHERE object_id IN (@d, @i)", ("d", a.Dalva.MembershipId), ("i", a.Iara.MembershipId)));
    }

    // CA-JOIN-006 b/c/d: outros cargos, membro de B, a si mesmo, o diretor, motivo ausente ou de outra operação.
    [Fact]
    public async Task CA_JOIN_006_Invalid_suspensions_are_denied()
    {
        var a = await TestData.CreateClubAAsync(db);
        var b = await TestData.CreateClubBAsync(db);
        await using var api = Api();
        using var caio = api.CreateClientAs(a.Caio);
        var reason = new MemberStatusRequest(ReasonCode.AdministrativeReview);

        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(a.Dalva).PostAsJsonAsync(ApiRoutes.SuspendMember(a.ClubId, a.Iara.MembershipId), reason),
            HttpStatusCode.Forbidden, ProblemCodes.PermissionDenied);
        Assert.Equal(HttpStatusCode.NotFound, (await caio.PostAsJsonAsync(ApiRoutes.SuspendMember(a.ClubId, b.Bento.MembershipId), reason)).StatusCode);
        await ProblemAssert.HasCodeAsync(await caio.PostAsJsonAsync(ApiRoutes.SuspendMember(a.ClubId, a.Caio.MembershipId), reason),
            HttpStatusCode.Forbidden, ProblemCodes.SelfAction);
        await ProblemAssert.HasCodeAsync(await caio.PostAsJsonAsync(ApiRoutes.SuspendMember(a.ClubId, a.Aurora.MembershipId), reason),
            HttpStatusCode.Forbidden, ProblemCodes.MembershipTargetDirector);
        await ProblemAssert.HasCodeAsync(await caio.PostAsJsonAsync(ApiRoutes.RemoveMember(a.ClubId, a.Aurora.MembershipId), new MemberStatusRequest(ReasonCode.AdministrativeClosure)),
            HttpStatusCode.Forbidden, ProblemCodes.MembershipTargetDirector);
        await ProblemAssert.HasCodeAsync(await caio.PostAsJsonAsync(ApiRoutes.SuspendMember(a.ClubId, a.Iara.MembershipId), new MemberStatusRequest(null)),
            HttpStatusCode.BadRequest, ProblemCodes.ReasonRequired);
        await ProblemAssert.HasCodeAsync(await caio.PostAsJsonAsync(ApiRoutes.SuspendMember(a.ClubId, a.Iara.MembershipId), new MemberStatusRequest(ReasonCode.RemovalRequested)),
            HttpStatusCode.BadRequest, ProblemCodes.ReasonRequired);
    }

    // CA-JOIN-008 a, D-151: reativa a suspensa com motivo e consentimento vigente, sem restaurar cargos; repetir não duplica.
    [Fact]
    public async Task CA_JOIN_008_Reactivation_returns_without_roles()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);
        await aurora.PostAsJsonAsync(ApiRoutes.SuspendMember(a.ClubId, a.Lina.MembershipId), new MemberStatusRequest(ReasonCode.TemporaryLeave));

        using var reactivated = await aurora.PostAsJsonAsync(ApiRoutes.ReactivateMember(a.ClubId, a.Lina.MembershipId), new MemberStatusRequest(ReasonCode.ReturnAfterLeave));
        using var again = await aurora.PostAsJsonAsync(ApiRoutes.ReactivateMember(a.ClubId, a.Lina.MembershipId), new MemberStatusRequest(ReasonCode.ReturnAfterLeave));

        Assert.Equal(HttpStatusCode.NoContent, reactivated.StatusCode);
        await ProblemAssert.HasCodeAsync(again, HttpStatusCode.Conflict, ProblemCodes.MembershipNotSuspended);
        Assert.Equal("Active", await StatusAsync(a.Lina.MembershipId));
        Assert.Equal(0, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM role_assignments WHERE membership_id = @m AND ended_at IS NULL", ("m", a.Lina.MembershipId)));
        Assert.Equal(1, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM audit_entries WHERE target_id = @m AND action = 'MembershipReactivated'", ("m", a.Lina.MembershipId)));
    }

    // CA-PRV-008, CA-JOIN-008 d: a revogação que termina enquanto a reativação espera a trava do consentimento vence; a
    // reativação relê o consentimento depois da trava e a participação continua suspensa.
    [Fact]
    public async Task CA_PRV_008_Revocation_finished_while_reactivation_waits_wins()
    {
        var a = await TestData.CreateClubAAsync(db);
        var suspended = await TestData.AddMemberAsync(db, a.ClubId, await TestData.CreatePersonAsync(db, "Suspensa Disputa"), MembershipStatus.Suspended);
        var consent = await OwnerSql.ScalarAsync<Guid>(db.OwnerConnectionString,
            "SELECT id FROM consents WHERE person_id = @p AND club_id = @c", ("p", suspended.PersonId), ("c", a.ClubId));
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);

        await using (var hold = await LockHold.ForUpdateAsync(db, "consents", consent))
        {
            var reactivation = aurora.PostAsJsonAsync(ApiRoutes.ReactivateMember(a.ClubId, suspended.MembershipId),
                new MemberStatusRequest(ReasonCode.ReturnAfterLeave));
            await hold.WaitForBlockedAsync();
            await hold.ExecuteAsync("UPDATE consents SET revoked_at = now() WHERE id = @id", ("id", consent));
            await hold.CommitAsync();

            await ProblemAssert.HasCodeAsync(await reactivation, HttpStatusCode.Forbidden, ProblemCodes.ConsentRequired);
        }

        Assert.Equal("Suspended", await StatusAsync(suspended.MembershipId));
    }

    // CA-JOIN-008 b/c/d: outros cargos, B, a si mesmo, encerrada, sem motivo e consentimento revogado (S-086).
    [Fact]
    public async Task CA_JOIN_008_Invalid_reactivations_are_denied()
    {
        var a = await TestData.CreateClubAAsync(db);
        var b = await TestData.CreateClubBAsync(db);
        var ended = await TestData.AddMemberAsync(db, a.ClubId, await TestData.CreatePersonAsync(db, "Encerrada"), MembershipStatus.Ended);
        var revoked = await TestData.AddMemberAsync(db, a.ClubId, await TestData.CreatePersonAsync(db, "Revogou"), MembershipStatus.Suspended);
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, "UPDATE consents SET revoked_at = now() WHERE person_id = @p", ("p", revoked.PersonId));
        var suspended = await TestData.AddMemberAsync(db, a.ClubId, await TestData.CreatePersonAsync(db, "Suspensa"), MembershipStatus.Suspended);
        var bSuspended = await TestData.AddMemberAsync(db, b.ClubId, await TestData.CreatePersonAsync(db, "Suspensa B"), MembershipStatus.Suspended);
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);
        var reason = new MemberStatusRequest(ReasonCode.SuspensionCorrection);

        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(a.Lina).PostAsJsonAsync(ApiRoutes.ReactivateMember(a.ClubId, suspended.MembershipId), reason),
            HttpStatusCode.Forbidden, ProblemCodes.PermissionDenied);
        Assert.Equal(HttpStatusCode.NotFound, (await aurora.PostAsJsonAsync(ApiRoutes.ReactivateMember(a.ClubId, bSuspended.MembershipId), reason)).StatusCode);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.ReactivateMember(a.ClubId, a.Aurora.MembershipId), reason),
            HttpStatusCode.Forbidden, ProblemCodes.SelfAction);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.ReactivateMember(a.ClubId, ended.MembershipId), reason),
            HttpStatusCode.Conflict, ProblemCodes.MembershipEnded);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.ReactivateMember(a.ClubId, suspended.MembershipId), new MemberStatusRequest(null)),
            HttpStatusCode.BadRequest, ProblemCodes.ReasonRequired);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.ReactivateMember(a.ClubId, revoked.MembershipId), reason),
            HttpStatusCode.Forbidden, ProblemCodes.ConsentRequired);
    }

    // CA-JOIN-008 d, RN-TEN-006: com outra participação ativa (em B ou nova em A), a reativação é negada; a disputa com uma
    // aprovação simultânea cria no máximo uma ativa. Aqui a aprovação é simulada direto no banco; o índice
    // único de participação ativa é a garantia.
    [Fact]
    public async Task CA_JOIN_008_Other_active_membership_blocks_reactivation_even_under_race()
    {
        var a = await TestData.CreateClubAAsync(db);
        var b = await TestData.CreateClubBAsync(db);
        var person = await TestData.CreatePersonAsync(db, "Disputada");
        var old = await TestData.AddMemberAsync(db, a.ClubId, person, MembershipStatus.Suspended);
        await TestData.AddMemberAsync(db, b.ClubId, person);
        var person2 = await TestData.CreatePersonAsync(db, "Corrida");
        var old2 = await TestData.AddMemberAsync(db, a.ClubId, person2, MembershipStatus.Suspended);
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);
        var reason = new MemberStatusRequest(ReasonCode.ReturnAfterLeave);

        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.ReactivateMember(a.ClubId, old.MembershipId), reason),
            HttpStatusCode.Conflict, ProblemCodes.MembershipOtherActive);
        var approval = OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
            INSERT INTO memberships (id, club_id, person_id, kind, status, started_at) VALUES (@id, @c, @p, 'Leader', 'Active', now())
            ON CONFLICT DO NOTHING
            """, ("id", Guid.CreateVersion7()), ("c", b.ClubId), ("p", person2.PersonId));
        var reactivation = aurora.PostAsJsonAsync(ApiRoutes.ReactivateMember(a.ClubId, old2.MembershipId), reason);
        await Task.WhenAll(approval, reactivation);

        Assert.Equal(1, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM memberships WHERE person_id = @p AND status = 'Active'", ("p", person2.PersonId)));
    }
}
