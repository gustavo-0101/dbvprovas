using System.Net;
using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Sdk;

namespace Dbvprovas.Api.Tests.Shared;

// Confere o apoio de testes comum (LockHold, ProblemAssert, LeakProbe) e as travas de linha contra o RLS.
public sealed class SharedSupportTests(PostgresFixture db)
{
    // RNF-AUD-007, D-140: a sessão do app espera a trava segurada pelo dono e, ao seguir, enxerga o estado já confirmado.
    [Fact]
    public async Task Membership_lock_waits_for_the_holder_and_sees_the_committed_state()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var hold = await LockHold.ForUpdateAsync(db, "memberships", a.Iara.MembershipId);
        await using var services = DbScope.Create(db);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubContext>();
        context.ClubId = a.ClubId;
        context.PersonId = a.Aurora.PersonId;
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var tx = await app.Database.BeginTransactionAsync();

        var locking = RowLocks.LockMembershipsAsync(app, [a.Iara.MembershipId], CancellationToken.None);
        await hold.WaitForBlockedAsync();
        Assert.False(locking.IsCompleted);
        await hold.ExecuteAsync("UPDATE memberships SET status = 'Suspended', suspended_at = now() WHERE id = @id", ("id", a.Iara.MembershipId));
        await hold.CommitAsync();
        await locking;

        var status = await app.Memberships.Where(m => m.Id == a.Iara.MembershipId).Select(m => m.Status).SingleAsync();
        Assert.Equal(MembershipStatus.Suspended, status);
    }

    // RNF-AUD-007, D-140: o titular trava o consentimento para alterá-lo e a diretoria, para depender dele; as duas travas
    // passam pelo RLS e esperam quem já o segura.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Consent_lock_passes_row_security_and_waits_for_the_holder(bool exclusive)
    {
        var a = await TestData.CreateClubAAsync(db);
        var consentId = await OwnerSql.ScalarAsync<Guid>(db.OwnerConnectionString,
            "SELECT id FROM consents WHERE person_id = @p AND club_id = @c", ("p", a.Iara.PersonId), ("c", a.ClubId));
        await using var hold = await LockHold.ForUpdateAsync(db, "consents", consentId);
        await using var services = DbScope.Create(db);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubContext>();
        context.ClubId = a.ClubId;
        context.PersonId = exclusive ? a.Iara.PersonId : a.Aurora.PersonId;
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var tx = await app.Database.BeginTransactionAsync();

        var locking = RowLocks.LockConsentAsync(app, consentId, exclusive, CancellationToken.None);
        await hold.WaitForBlockedAsync();
        Assert.False(locking.IsCompleted);
        await hold.CommitAsync();
        await locking;
    }

    // D-125: o ProblemDetails sai com o "code" no topo do JSON, onde o ProblemAssert o procura.
    [Theory]
    [InlineData(OutcomeKind.Forbidden, HttpStatusCode.Forbidden, ProblemCodes.SelfAction)]
    [InlineData(OutcomeKind.Conflict, HttpStatusCode.Conflict, ProblemCodes.PendingRequestExists)]
    [InlineData(OutcomeKind.Invalid, HttpStatusCode.BadRequest, ProblemCodes.ValidationText)]
    public async Task Problem_results_carry_the_code_extension_in_the_body(OutcomeKind kind, HttpStatusCode status, string code)
    {
        var response = await ExecuteAsync(ApiProblems.For(kind, code));

        await ProblemAssert.HasCodeAsync(response, status, code);
    }

    [Fact]
    public async Task Problem_assert_refuses_a_different_code()
    {
        var response = await ExecuteAsync(ApiProblems.For(OutcomeKind.Conflict, ProblemCodes.StateConflict));

        await Assert.ThrowsAnyAsync<XunitException>(() =>
            ProblemAssert.HasCodeAsync(response, HttpStatusCode.Conflict, ProblemCodes.PendingRequestExists));
    }

    // CA-AUD-004: a sonda só vale se aceitar uma execução limpa e acusar um valor que esteja num dos canais.
    [Fact]
    public async Task Leak_probe_accepts_a_clean_run_and_flags_a_tracked_value_found_in_the_logs()
    {
        using var probe = new LeakProbe();
        await using var api = new ApiFactory(db, "Production");
        using var client = api.CreateClient();
        using (var ready = await client.GetAsync("/health/ready"))
        {
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            await ready.Content.ReadAsStringAsync();
        }

        probe.AssertAbsent(api);
        probe.Track("Microsoft.AspNetCore");

        Assert.ThrowsAny<XunitException>(() => probe.AssertAbsent(api));
    }

    private static async Task<HttpResponseMessage> ExecuteAsync(IResult result)
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        var body = new MemoryStream();
        http.Response.Body = body;
        await result.ExecuteAsync(http);
        body.Position = 0;
        return new HttpResponseMessage((HttpStatusCode)http.Response.StatusCode) { Content = new StreamContent(body) };
    }
}
