using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Audit;
using Dbvprovas.Api.Modules.Joining;
using Dbvprovas.Api.Modules.Notifications;
using Dbvprovas.Api.Modules.Organization;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Api.Modules.Tenancy.Founding;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dbvprovas.Api.Tests.Shared;

public sealed class SharedServicesTests(PostgresFixture db)
{
    private static (AsyncServiceScope Scope, AppDbContext Db) InClub(ServiceProvider services, Guid clubId, Guid? personId = null)
    {
        var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubContext>();
        context.ClubId = clubId;
        context.PersonId = personId;
        return (scope, scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    // D-155, D-163, RN-AUD-001: encerrar todos os cargos audita cada um e, se for o diretor, cancela a indicação dele.
    [Fact]
    public async Task Role_termination_ends_roles_audits_and_cancels_director_nomination()
    {
        var a = await TestData.CreateClubAAsync(db);
        await TestData.AssignRoleAsync(db, a.Aurora, RoleCode.Chaplain);
        var nomination = Guid.CreateVersion7();
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
            INSERT INTO director_nominations (id, club_id, nominator_membership_id, nominee_membership_id, status, created_at, expires_at)
                VALUES (@id, @club, @aurora, @caio, 'Pending', now(), now() + interval '7 days')
            """, ("id", nomination), ("club", a.ClubId), ("aurora", a.Aurora.MembershipId), ("caio", a.Caio.MembershipId));
        await using var services = DbScope.Create(db);
        var (scope, app) = InClub(services, a.ClubId, a.Caio.PersonId);
        await using (scope)
        {
            var ended = await scope.ServiceProvider.GetRequiredService<RoleTermination>()
                .EndAllAsync(a.Aurora.MembershipId, EndCause.ConsentRevoked, a.Caio.PersonId, CancellationToken.None);
            await app.SaveChangesAsync();
            Assert.Equal(2, ended);
        }

        Assert.Equal(0, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM role_assignments WHERE membership_id = @m AND ended_at IS NULL", ("m", a.Aurora.MembershipId)));
        Assert.Equal(2, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM audit_entries WHERE action = 'RoleEnded' AND cause = 'ConsentRevoked' AND club_id = @c", ("c", a.ClubId)));
        Assert.Equal("Cancelled", await OwnerSql.ScalarAsync<string>(db.OwnerConnectionString,
            "SELECT status FROM director_nominations WHERE id = @id", ("id", nomination)));
    }

    // D-158: encerrar só alguns cargos mantém os demais.
    [Fact]
    public async Task Role_termination_by_predicate_keeps_other_roles()
    {
        var a = await TestData.CreateClubAAsync(db);
        await TestData.AssignRoleAsync(db, a.Caio, RoleCode.Chaplain);
        await using var services = DbScope.Create(db);
        var (scope, app) = InClub(services, a.ClubId, a.Aurora.PersonId);
        await using (scope)
        {
            await scope.ServiceProvider.GetRequiredService<RoleTermination>().EndAsync(a.Caio.MembershipId,
                role => role is RoleCode.Secretary or RoleCode.Treasurer, EndCause.BecameDirector, a.Aurora.PersonId, CancellationToken.None);
            await app.SaveChangesAsync();
        }

        Assert.Equal("Chaplain", await OwnerSql.ScalarAsync<string>(db.OwnerConnectionString,
            "SELECT role FROM role_assignments WHERE membership_id = @m AND ended_at IS NULL", ("m", a.Caio.MembershipId)));
    }

    // RN-JOIN-006, D-138: dois envios da mesma pessoa ao mesmo tempo; o segundo espera o primeiro e vê a pendência.
    [Fact]
    public async Task Pending_guard_serializes_the_same_person()
    {
        var person = await TestData.CreatePersonAsync(db, "Pendente");
        await using var services = DbScope.Create(db);
        var started = new TaskCompletionSource();

        async Task<bool> FirstAsync()
        {
            await using var scope = services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ClubContext>().PersonId = person.PersonId;
            var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var tx = await app.Database.BeginTransactionAsync();
            var acquired = await PendingRequestGuard.TryAcquireAsync(app, person.PersonId, DateTimeOffset.UtcNow, CancellationToken.None);
            started.SetResult();
            app.FoundingRequests.Add(new FoundingRequest
            {
                Id = Guid.CreateVersion7(), RequesterPersonId = person.PersonId, RequesterAccountId = person.AccountId,
                ClubName = "Clube Pendente", ClubCity = "Cidade", ProposedClubId = Guid.CreateVersion7(), CreatedAt = DateTimeOffset.UtcNow,
            });
            await Task.Delay(500);
            await app.SaveChangesAsync();
            await tx.CommitAsync();
            return acquired;
        }

        async Task<bool> SecondAsync()
        {
            await started.Task;
            await using var scope = services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ClubContext>().PersonId = person.PersonId;
            var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var tx = await app.Database.BeginTransactionAsync();
            return await PendingRequestGuard.TryAcquireAsync(app, person.PersonId, DateTimeOffset.UtcNow, CancellationToken.None);
        }

        var results = await Task.WhenAll(FirstAsync(), SecondAsync());

        Assert.Equal(new[] { true, false }, results);
    }

    // D-011, PC-06: a diretoria que recebe avisos tem cargo de diretoria vigente, participação ativa e 18+.
    [Fact]
    public async Task Notifier_board_includes_only_adult_active_board_members()
    {
        var a = await TestData.CreateClubAAsync(db);
        await TestData.CreateMemberAsync(db, a.ClubId, "Menor", age: 17, role: RoleCode.AssociateDirector);
        var suspended = await TestData.AddMemberAsync(db, a.ClubId, await TestData.CreatePersonAsync(db, "Suspensa"), MembershipStatus.Suspended);
        await TestData.AssignRoleAsync(db, suspended, RoleCode.Chaplain);
        await using var services = DbScope.Create(db);
        var (scope, _) = InClub(services, a.ClubId);
        await using (scope)
        {
            var notifier = scope.ServiceProvider.GetRequiredService<Notifier>();
            var board = await notifier.BoardAsync(CancellationToken.None);
            var directorAndSecretary = await notifier.DirectorAndSecretaryAsync(CancellationToken.None);

            Assert.Equal(new[] { a.Aurora.PersonId, a.Caio.PersonId, a.Dalva.PersonId }.Order(), board.Order());
            Assert.Equal(new[] { a.Aurora.PersonId, a.Caio.PersonId }.Order(), directorAndSecretary.Order());
        }
    }

    // D-146: aviso de admin só para quem tem a marca e 18 anos ou mais.
    [Fact]
    public async Task Notifier_platform_admins_are_adults_only()
    {
        var adult = await TestData.CreatePersonAsync(db, "Admin Adulta", age: 18, isPlatformAdmin: true);
        var minor = await TestData.CreatePersonAsync(db, "Admin Menor", age: 17, isPlatformAdmin: true);
        await using var services = DbScope.Create(db);
        await using var scope = services.CreateAsyncScope();

        var admins = await scope.ServiceProvider.GetRequiredService<Notifier>().PlatformAdminsAsync(CancellationToken.None);

        Assert.Contains(adult.PersonId, admins);
        Assert.DoesNotContain(minor.PersonId, admins);
    }

    // CA-NTF-001: o mesmo aviso para o mesmo destinatário e a mesma ocorrência não se repete (o banco recusa).
    [Fact]
    public async Task Notification_is_unique_per_recipient_event_and_object()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var services = DbScope.Create(db);
        var (scope, app) = InClub(services, a.ClubId, a.Aurora.PersonId);
        await using (scope)
        {
            var notifier = scope.ServiceProvider.GetRequiredService<Notifier>();
            var occurrence = Guid.CreateVersion7();
            notifier.Notify(NotificationEvent.JoinQueued, occurrence, a.ClubId, [a.Iara.PersonId]);
            await app.SaveChangesAsync();
            notifier.Notify(NotificationEvent.JoinQueued, occurrence, a.ClubId, [a.Iara.PersonId]);

            var error = await Assert.ThrowsAsync<DbUpdateException>(() => app.SaveChangesAsync());
            Assert.True(DbErrors.IsUniqueViolation(error, DbConstraints.NotificationOnce));
            Assert.False(DbErrors.IsUniqueViolation(error, DbConstraints.EmailOnce));
        }
    }

    // CA-AUD-002, D-160: o registro de auditoria não tem campo de texto livre; só IDs, códigos e a lista de campos.
    [Fact]
    public void Audit_entry_has_no_free_text_field()
    {
        var textProperties = typeof(AuditEntry).GetProperties().Where(p => p.PropertyType == typeof(string)).Select(p => p.Name);

        Assert.Empty(textProperties);
    }

    // CA-AUD-010: auditoria e decisão no mesmo SaveChanges; se a auditoria falha, nada fica.
    [Fact]
    public async Task Audit_and_change_are_saved_together()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var services = DbScope.Create(db);
        var (scope, app) = InClub(services, a.ClubId, a.Aurora.PersonId);
        await using (scope)
        {
            var unit = await app.Units.SingleAsync(u => u.Id == a.UnitY);
            unit.Name = "Nome Novo";
            var otherClub = Guid.CreateVersion7();
            scope.ServiceProvider.GetRequiredService<AuditLog>().Record(AuditAction.RoleAssigned, AuditTarget.Membership,
                Guid.CreateVersion7(), otherClub, AuditScope.Club, a.Aurora.PersonId);

            await Assert.ThrowsAsync<ClubIsolationException>(() => app.SaveChangesAsync());
        }

        Assert.Equal("Unidade Y", await OwnerSql.ScalarAsync<string>(db.OwnerConnectionString, "SELECT name FROM units WHERE id = @u", ("u", a.UnitY)));
    }

    // D-125: o resultado de domínio vira o contrato HTTP do R1.
    [Fact]
    public void Outcome_maps_to_the_http_contract()
    {
        Assert.IsType<NoContent>(Outcome.Done.ToHttp().Result);
        Assert.IsType<NotFound>(Outcome.NotFound.ToHttp().Result);
        var forbidden = Assert.IsType<ProblemHttpResult>(Outcome.Forbidden(ProblemCodes.SelfAction).ToHttp().Result);
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Equal(ProblemCodes.SelfAction, forbidden.ProblemDetails.Extensions[ProblemCodes.Extension]);
        Assert.Equal(409, Assert.IsType<ProblemHttpResult>(Outcome.Conflict().ToHttp().Result).StatusCode);
        Assert.Equal(400, Assert.IsType<ProblemHttpResult>(Outcome.Invalid(ProblemCodes.ReasonRequired).ToHttp().Result).StatusCode);
        Assert.Equal(5, Assert.IsType<Ok<int>>(Outcome<int>.Ok(5).ToHttp().Result).Value);
        Outcome<int> failed = Outcome.Conflict(ProblemCodes.PendingRequestExists);
        Assert.Equal(409, Assert.IsType<ProblemHttpResult>(failed.ToHttp().Result).StatusCode);
    }

    // RNF-AUD-007: a trava sem transação não serve, então é recusada.
    [Fact]
    public async Task Row_locks_require_a_transaction()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var services = DbScope.Create(db);
        var (scope, app) = InClub(services, a.ClubId);
        await using (scope)
            await Assert.ThrowsAsync<InvalidOperationException>(() => RowLocks.LockMembershipsAsync(app, [a.Aurora.MembershipId], CancellationToken.None));
    }
}
