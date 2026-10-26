using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Audit;
using Dbvprovas.Api.Modules.Notifications;
using Dbvprovas.Api.Modules.Organization;
using Dbvprovas.Api.Modules.Privacy;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Api.Modules.Tenancy.Founding;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dbvprovas.Api.Tests.Tenancy;

// CA-TEN-001 (variantes R1): as duas barreiras cobrem todas as tabelas novas. Cada teste cria os próprios clubes.
public sealed class R1IsolationTests(PostgresFixture db)
{
    private static readonly string[] ClubTables =
        ["units", "role_assignments", "director_nominations", "join_requests", "consents", "admin_access_requests", "audit_entries"];

    private sealed record World(TestClubA A, TestClubB B, TestPerson Admin, Guid JoinA, Guid JoinB, Guid FoundingA, Guid Requester);

    // Uma linha de cada tabela nova em A e em B, mais um pedido de fundação de uma pessoa de A.
    private async Task<World> CreateWorldAsync()
    {
        var a = await TestData.CreateClubAAsync(db);
        var b = await TestData.CreateClubBAsync(db);
        var admin = await TestData.CreateAdminAsync(db);
        var joinA = await QueueJoinAsync(a.ClubId);
        var joinB = await QueueJoinAsync(b.ClubId);
        foreach (var (club, nominator, nominee) in new[] { (a.ClubId, a.Aurora.MembershipId, a.Caio.MembershipId), (b.ClubId, b.Bento.MembershipId, b.Bento.MembershipId) })
            await OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
                INSERT INTO director_nominations (id, club_id, nominator_membership_id, nominee_membership_id, status, created_at, expires_at)
                    VALUES (@id, @club, @nominator, @nominee, 'Pending', now(), now() + interval '7 days');
                INSERT INTO admin_access_requests (id, club_id, requested_by_person_id, reason_code, target_membership_ids, fields, status, created_at, expires_at)
                    VALUES (@access, @club, @admin, 'ClubSupportRequest', ARRAY[@nominee]::uuid[], ARRAY['AdultProfile'], 'Pending', now(), now() + interval '7 days');
                INSERT INTO audit_entries (id, club_id, scope, actor_person_id, action, target_type, target_id, fields, occurred_at)
                    VALUES (@audit, @club, 'Club', NULL, 'RoleAssigned', 'Membership', @nominee, '{}', now());
                """,
                ("id", Guid.CreateVersion7()), ("club", club), ("nominator", nominator), ("nominee", nominee),
                ("access", Guid.CreateVersion7()), ("admin", admin.PersonId), ("audit", Guid.CreateVersion7()));
        var requester = await TestData.CreatePersonAsync(db, "Solicitante");
        var founding = Guid.CreateVersion7();
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
            INSERT INTO founding_requests (id, requester_person_id, requester_account_id, club_name, club_city, proposed_club_id,
                    status, exception_requested, exception_granted, created_at)
                VALUES (@id, @person, @account, 'Clube Novo', 'Cidade Nova', @proposed, 'Pending', false, false, now());
            INSERT INTO consents (id, person_id, kind, founding_request_id, term_version, granted_at)
                VALUES (@consent, @person, 'OwnParticipation', @id, 'v', now());
            INSERT INTO audit_entries (id, club_id, scope, action, target_type, target_id, fields, occurred_at)
                VALUES (@audit, NULL, 'Platform', 'FoundingRequested', 'FoundingRequest', @id, '{}', now());
            """,
            ("id", founding), ("person", requester.PersonId), ("account", requester.AccountId), ("proposed", Guid.CreateVersion7()),
            ("consent", Guid.CreateVersion7()), ("audit", Guid.CreateVersion7()));
        return new World(a, b, admin, joinA, joinB, founding, requester.PersonId);
    }

    private async Task<Guid> QueueJoinAsync(Guid clubId)
    {
        var candidate = await TestData.CreatePersonAsync(db, "Candidata");
        var consent = Guid.CreateVersion7();
        var request = Guid.CreateVersion7();
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
            INSERT INTO consents (id, person_id, kind, club_id, term_version, granted_at) VALUES (@consent, @person, 'OwnParticipation', @club, 'v', now());
            INSERT INTO join_requests (id, club_id, candidate_person_id, created_by_account_id, kind, path, status, consent_id, queued_at, expires_at)
                VALUES (@request, @club, @person, @account, 'Leader', 'Search', 'Queued', @consent, now(), now() + interval '14 days');
            """,
            ("consent", consent), ("person", candidate.PersonId), ("club", clubId), ("account", candidate.AccountId), ("request", request));
        return request;
    }

    private static async Task<NpgsqlConnection> AppConnectionAsync(string connectionString, Guid? club, Guid? person, bool admin = false)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.club_id', @c, false), set_config('app.person_id', @p, false), set_config('app.platform_admin', @a, false)", connection);
        command.Parameters.AddWithValue("c", club?.ToString() ?? "");
        command.Parameters.AddWithValue("p", person?.ToString() ?? "");
        command.Parameters.AddWithValue("a", admin ? "true" : "");
        await command.ExecuteNonQueryAsync();
        return connection;
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> ExecuteAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> TextAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return await command.ExecuteScalarAsync() as string;
    }

    [Fact]
    public async Task CA_TEN_001_R1_database_hides_other_club_rows_with_filters_off()
    {
        var world = await CreateWorldAsync();
        await using var connection = await AppConnectionAsync(db.AppConnectionString, world.A.ClubId, world.A.Aurora.PersonId);

        foreach (var table in ClubTables)
        {
            Assert.Equal(0, await CountAsync(connection, $"SELECT count(*) FROM {table} WHERE club_id = @b", ("b", world.B.ClubId)));
            Assert.True(await CountAsync(connection, $"SELECT count(*) FROM {table} WHERE club_id = @a", ("a", world.A.ClubId)) > 0, table);
        }
    }

    [Fact]
    public async Task CA_TEN_001_R1_database_rejects_writes_into_other_club()
    {
        var world = await CreateWorldAsync();
        await using var connection = await AppConnectionAsync(db.AppConnectionString, world.A.ClubId, world.A.Aurora.PersonId);
        var inserts = new[]
        {
            "INSERT INTO units (id, club_id, name, sex, min_age, max_age, status, created_at) VALUES (gen_random_uuid(), @b, 'U', 'Male', 10, 12, 'Active', now())",
            "INSERT INTO role_assignments (id, club_id, membership_id, role, started_at) VALUES (gen_random_uuid(), @b, @m, 'Chaplain', now())",
            "INSERT INTO director_nominations (id, club_id, nominator_membership_id, nominee_membership_id, status, created_at, expires_at) VALUES (gen_random_uuid(), @b, @m, @m, 'Declined', now(), now())",
            "INSERT INTO audit_entries (id, club_id, scope, action, target_type, target_id, fields, occurred_at) VALUES (gen_random_uuid(), @b, 'Club', 'RoleAssigned', 'Membership', @m, '{}', now())",
            "INSERT INTO clubs (id, name, city, invite_code, status, created_at) VALUES (@b2, 'X', 'Y', 'ZZZZ2222', 'Active', now())",
        };

        foreach (var sql in inserts)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("b", world.B.ClubId);
            command.Parameters.AddWithValue("b2", Guid.CreateVersion7());
            command.Parameters.AddWithValue("m", world.B.Bento.MembershipId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }

        await using var update = new NpgsqlCommand("UPDATE units SET name = 'X' WHERE club_id = @b", connection);
        update.Parameters.AddWithValue("b", world.B.ClubId);
        Assert.Equal(0, await update.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task CA_TEN_001_R1_without_context_reads_nothing()
    {
        await CreateWorldAsync();
        await using var connection = await AppConnectionAsync(db.AppConnectionString, null, null);

        foreach (var table in ClubTables.Concat(["clubs", "memberships", "persons", "founding_requests", "founding_records", "notifications"]))
            Assert.Equal(0, await CountAsync(connection, $"SELECT count(*) FROM {table}"));
    }

    // Sem contexto, nenhuma escrita passa, nem a auditoria de plataforma nem o aviso (CA-TEN-001 R1, falha fechada).
    [Fact]
    public async Task CA_TEN_001_R1_without_context_writes_nothing()
    {
        var world = await CreateWorldAsync();
        await using var connection = await AppConnectionAsync(db.AppConnectionString, null, null);
        var inserts = new[]
        {
            "INSERT INTO audit_entries (id, club_id, scope, action, target_type, target_id, fields, occurred_at) VALUES (gen_random_uuid(), NULL, 'Platform', 'FoundingRequested', 'FoundingRequest', @t, '{}', now())",
            "INSERT INTO notifications (id, recipient_person_id, event, club_id, object_id, created_at) VALUES (gen_random_uuid(), @p, 'FoundingApproved', NULL, @t, now())",
        };

        foreach (var sql in inserts)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("t", world.FoundingA);
            command.Parameters.AddWithValue("p", world.A.Iara.PersonId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }
    }

    // As referências ficam no mesmo clube e no mesmo titular, também para o banco: participação, unidade ou consentimento de
    // outro clube, ou consentimento de outra pessoa, são recusados mesmo dentro do clube da requisição (RN-TEN-001).
    [Fact]
    public async Task CA_TEN_001_R1_references_stay_in_the_same_club_and_person()
    {
        var world = await CreateWorldAsync();
        var candidate = await TestData.CreatePersonAsync(db, "Candidato Sem Termo");
        var other = await TestData.CreatePersonAsync(db, "Outra Pessoa");
        var otherConsent = Guid.CreateVersion7();
        var clubBConsent = Guid.CreateVersion7();
        var ownConsent = Guid.CreateVersion7();
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
            INSERT INTO consents (id, person_id, kind, club_id, term_version, granted_at) VALUES (@o, @other, 'OwnParticipation', @a, 'v', now());
            INSERT INTO consents (id, person_id, kind, club_id, term_version, granted_at) VALUES (@cb, @candidate, 'OwnParticipation', @b, 'v', now());
            INSERT INTO consents (id, person_id, kind, club_id, term_version, granted_at) VALUES (@own, @candidate, 'OwnParticipation', @a, 'v', now());
            """,
            ("o", otherConsent), ("other", other.PersonId), ("a", world.A.ClubId), ("cb", clubBConsent), ("candidate", candidate.PersonId),
            ("b", world.B.ClubId), ("own", ownConsent));
        await using var connection = await AppConnectionAsync(db.AppConnectionString, world.A.ClubId, world.A.Aurora.PersonId);
        const string Join = "INSERT INTO join_requests (id, club_id, candidate_person_id, created_by_account_id, kind, path, status, consent_id, queued_at, expires_at) VALUES (gen_random_uuid(), @a, @candidate, @account, 'Leader', 'Search', 'Queued', @consent, now(), now() + interval '14 days')";
        var refused = new (string Sql, Guid Consent)[]
        {
            ("INSERT INTO role_assignments (id, club_id, membership_id, role, started_at) VALUES (gen_random_uuid(), @a, @mb, 'Chaplain', now())", Guid.Empty),
            ("INSERT INTO role_assignments (id, club_id, membership_id, role, unit_id, started_at) VALUES (gen_random_uuid(), @a, @lina, 'AssociateCounselor', @zb, now())", Guid.Empty),
            ("INSERT INTO director_nominations (id, club_id, nominator_membership_id, nominee_membership_id, status, created_at, expires_at) VALUES (gen_random_uuid(), @a, @aurora, @mb, 'Declined', now(), now())", Guid.Empty),
            (Join, otherConsent),
            (Join, clubBConsent),
        };
        (string, object)[] Parameters(Guid consent) =>
        [
            ("a", world.A.ClubId), ("mb", world.B.Bento.MembershipId), ("lina", world.A.Lina.MembershipId), ("zb", world.B.UnitZ),
            ("aurora", world.A.Aurora.MembershipId), ("candidate", candidate.PersonId), ("account", candidate.AccountId), ("consent", consent),
        ];

        foreach (var (sql, consent) in refused)
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, sql, Parameters(consent)));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        }

        Assert.Equal(1, await ExecuteAsync(connection, Join, Parameters(ownConsent)));
    }

    // A segunda barreira também cobre quem decide: membro sem cargo não autoriza o acesso do admin nem mexe no consentimento
    // alheio; o solicitante só cancela o próprio pedido de fundação (CA-AUD-009, CA-PRV-008, CA-TEN-009).
    [Fact]
    public async Task CA_TEN_001_R1_database_checks_who_decides()
    {
        var world = await CreateWorldAsync();
        await using var iara = await AppConnectionAsync(db.AppConnectionString, world.A.ClubId, world.A.Iara.PersonId);
        await using var aurora = await AppConnectionAsync(db.AppConnectionString, world.A.ClubId, world.A.Aurora.PersonId);
        await using var requester = await AppConnectionAsync(db.AppConnectionString, null, world.Requester);

        Assert.Equal(0, await ExecuteAsync(iara, "UPDATE admin_access_requests SET status = 'Authorized' WHERE club_id = @a", ("a", world.A.ClubId)));
        var alien = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync(iara, "UPDATE consents SET revoked_at = now() WHERE person_id = @p", ("p", world.A.Aurora.PersonId)));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, alien.SqlState);
        var forged = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync(requester, "UPDATE founding_requests SET status = 'Approved' WHERE id = @f", ("f", world.FoundingA)));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, forged.SqlState);
        Assert.Equal(1, await ExecuteAsync(aurora, "UPDATE admin_access_requests SET status = 'Denied' WHERE club_id = @a", ("a", world.A.ClubId)));
        Assert.Equal(1, await ExecuteAsync(requester, "UPDATE founding_requests SET status = 'Cancelled' WHERE id = @f", ("f", world.FoundingA)));
    }

    // A visão do admin e os prazos agendados não abrem para quem não é admin nem a tarefa agendada (CA-TEN-010 c, RNF-TEN-006).
    [Fact]
    public async Task CA_TEN_001_R1_admin_overview_and_due_clubs_need_their_context()
    {
        var world = await CreateWorldAsync();
        await using var member = await AppConnectionAsync(db.AppConnectionString, world.A.ClubId, world.A.Iara.PersonId);
        await using var admin = await AppConnectionAsync(db.AppConnectionString, null, world.Admin.PersonId, admin: true);
        await using var job = await AppConnectionAsync(db.AppConnectionString, null, null);
        const string Due = "SELECT count(*) FROM join_request_clubs_due(now() + interval '30 days', interval '48 hours', interval '30 days')";

        Assert.Equal(0, await CountAsync(member, "SELECT count(*) FROM admin_club_overview"));
        Assert.True(await CountAsync(admin, "SELECT count(*) FROM admin_club_overview") > 0);
        Assert.Equal(0, await CountAsync(member, Due));
        Assert.True(await CountAsync(job, Due) > 0);
    }

    // E-mail só pelo caminho que o usa: o próprio, ou o admin dentro do clube do membro (RN-PRV-001, PERM-AUD-002).
    [Fact]
    public async Task Personal_account_columns_open_only_through_their_functions()
    {
        var world = await CreateWorldAsync();
        await using var iara = await AppConnectionAsync(db.AppConnectionString, world.A.ClubId, world.A.Iara.PersonId);
        await using var admin = await AppConnectionAsync(db.AppConnectionString, world.A.ClubId, world.Admin.PersonId, admin: true);

        var denied = await Assert.ThrowsAsync<PostgresException>(() => TextAsync(iara, "SELECT email FROM accounts LIMIT 1"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        Assert.Equal($"p{world.A.Iara.PersonId:N}@example.com", await TextAsync(iara, "SELECT account_email(@p)", ("p", world.A.Iara.PersonId)));
        Assert.Null(await TextAsync(iara, "SELECT account_email(@p)", ("p", world.A.Caio.PersonId)));
        Assert.Equal($"p{world.A.Caio.PersonId:N}@example.com", await TextAsync(admin, "SELECT account_email(@p)", ("p", world.A.Caio.PersonId)));
        Assert.Null(await TextAsync(admin, "SELECT account_email(@p)", ("p", world.B.Bento.PersonId)));
    }

    // Sem clube, a pessoa vê só o próprio: cargos (via participação), pedidos e consentimentos.
    [Fact]
    public async Task CA_TEN_001_R1_own_rows_are_visible_without_club_context()
    {
        var world = await CreateWorldAsync();
        await using var connection = await AppConnectionAsync(db.AppConnectionString, null, world.A.Caio.PersonId);

        Assert.Equal(1, await CountAsync(connection, "SELECT count(*) FROM role_assignments"));
        Assert.Equal(1, await CountAsync(connection, "SELECT count(*) FROM consents"));
        Assert.Equal(0, await CountAsync(connection, "SELECT count(*) FROM join_requests"));
        Assert.Equal(0, await CountAsync(connection, "SELECT count(*) FROM units"));
    }

    // O admin, sem clube, vê os registros de plataforma, os pedidos de fundação e o mínimo dos solicitantes (RN-AUD-006);
    // não vê a auditoria interna do clube nem outras pessoas.
    [Fact]
    public async Task CA_TEN_001_R1_platform_admin_sees_only_platform_rows()
    {
        var world = await CreateWorldAsync();
        await using var connection = await AppConnectionAsync(db.AppConnectionString, null, world.Admin.PersonId, admin: true);

        Assert.True(await CountAsync(connection, "SELECT count(*) FROM founding_requests WHERE id = @f", ("f", world.FoundingA)) == 1);
        Assert.Equal(0, await CountAsync(connection, "SELECT count(*) FROM audit_entries WHERE scope = 'Club'"));
        Assert.True(await CountAsync(connection, "SELECT count(*) FROM audit_entries WHERE scope = 'Platform'") > 0);
        Assert.Equal(0, await CountAsync(connection, "SELECT count(*) FROM persons WHERE id = @p", ("p", world.A.Iara.PersonId)));
        Assert.Equal(0, await CountAsync(connection, "SELECT count(*) FROM units"));
    }

    // O filtro do EF sozinho (papel dono, sem RLS) também separa os clubes nas tabelas novas (D-124).
    [Fact]
    public async Task CA_TEN_001_R1_ef_filter_hides_other_club_rows_without_rls()
    {
        var world = await CreateWorldAsync();
        var context = new ClubContext { ClubId = world.A.ClubId, PersonId = world.A.Aurora.PersonId };
        await using var owner = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db.OwnerConnectionString).Options, context);
        var b = world.B.ClubId;

        Assert.Equal(0, await owner.Units.CountAsync(x => x.ClubId == b));
        Assert.Equal(0, await owner.RoleAssignments.CountAsync(x => x.ClubId == b));
        Assert.Equal(0, await owner.DirectorNominations.CountAsync(x => x.ClubId == b));
        Assert.Equal(0, await owner.JoinRequests.CountAsync(x => x.ClubId == b));
        Assert.Equal(0, await owner.AdminAccessRequests.CountAsync(x => x.ClubId == b));
        Assert.Equal(0, await owner.Consents.CountAsync(x => x.ClubId == b));
        Assert.Equal(0, await owner.AuditEntries.CountAsync(x => x.ClubId == b));
        Assert.True(await owner.Units.CountAsync() > 0);
    }

    // Sem contexto, o filtro do EF não deixa passar linha com clube nulo (consentimento pré-fundação, auditoria de plataforma).
    [Fact]
    public async Task CA_TEN_001_R1_ef_filter_does_not_match_null_club_without_context()
    {
        var world = await CreateWorldAsync();
        var context = new ClubContext();
        await using var owner = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db.OwnerConnectionString).Options, context);

        Assert.Equal(0, await owner.Consents.CountAsync(c => c.FoundingRequestId == world.FoundingA));
        Assert.Equal(0, await owner.AuditEntries.CountAsync(a => a.TargetId == world.FoundingA));
        context.IsPlatformAdmin = true;
        context.PersonId = world.Admin.PersonId;
        Assert.Equal(1, await owner.Consents.CountAsync(c => c.FoundingRequestId == world.FoundingA));
        Assert.Equal(1, await owner.AuditEntries.CountAsync(a => a.TargetId == world.FoundingA));
    }

    // A troca explícita muda as duas barreiras na mesma conexão e recusa trocar com mudança pendente (RNF-TEN-006).
    [Fact]
    public async Task CA_TEN_001_Club_scope_switch_changes_both_barriers()
    {
        var world = await CreateWorldAsync();
        await using var services = DbScope.Create(db);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubContext>();
        context.ClubId = world.A.ClubId;
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clubScope = scope.ServiceProvider.GetRequiredService<ClubScope>();
        await using var tx = await app.Database.BeginTransactionAsync();

        Assert.Equal(new[] { world.A.UnitX, world.A.UnitY }.Order(), (await app.Units.IgnoreQueryFilters().Select(u => u.Id).ToListAsync()).Order());
        await clubScope.EnterAsync(world.B.ClubId, CancellationToken.None);
        Assert.Equal([world.B.UnitZ], await app.Units.IgnoreQueryFilters().Select(u => u.Id).ToListAsync());
        Assert.Equal([world.B.UnitZ], await app.Units.Select(u => u.Id).ToListAsync());

        app.Units.Add(new Unit { Id = Guid.CreateVersion7(), ClubId = world.B.ClubId, Name = "Nova", Sex = Sex.Male, MinAge = 10, MaxAge = 12 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => clubScope.EnterAsync(world.A.ClubId, CancellationToken.None));
    }

    // Troca que falha no banco (aqui, cancelada) deixa as duas barreiras no clube de antes: o filtro do EF não vai para B
    // enquanto o RLS fica em A (RNF-TEN-006, CA-TEN-001).
    [Fact]
    public async Task CA_TEN_001_Failed_club_scope_switch_keeps_both_barriers_on_the_previous_club()
    {
        var world = await CreateWorldAsync();
        await using var services = DbScope.Create(db);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubContext>();
        context.ClubId = world.A.ClubId;
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clubScope = scope.ServiceProvider.GetRequiredService<ClubScope>();
        await using var tx = await app.Database.BeginTransactionAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clubScope.EnterAsync(world.B.ClubId, new CancellationToken(canceled: true)));

        Assert.Equal(world.A.ClubId, context.ClubId);
        Assert.Equal(new[] { world.A.UnitX, world.A.UnitY }.Order(), (await app.Units.IgnoreQueryFilters().Select(u => u.Id).ToListAsync()).Order());
    }

    // Entidade opcionalmente de clube com outro clube é barrada pelo guarda; sem clube, passa (RN-TEN-001).
    [Fact]
    public async Task CA_TEN_001_Write_guard_checks_optional_club_entities()
    {
        var world = await CreateWorldAsync();
        await using var services = DbScope.Create(db);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubContext>();
        context.ClubId = world.A.ClubId;
        context.PersonId = world.A.Iara.PersonId;
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        app.AuditEntries.Add(new AuditEntry
        {
            Id = Guid.CreateVersion7(), ClubId = world.B.ClubId, Scope = AuditScope.Club, Action = AuditAction.RoleAssigned,
            TargetType = AuditTarget.Membership, TargetId = Guid.CreateVersion7(), OccurredAt = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<ClubIsolationException>(() => app.SaveChangesAsync());
        app.ChangeTracker.Clear();

        var platform = new AuditEntry
        {
            Id = Guid.CreateVersion7(), ClubId = null, Scope = AuditScope.Platform, Action = AuditAction.FoundingRequested,
            TargetType = AuditTarget.FoundingRequest, TargetId = world.FoundingA, OccurredAt = DateTimeOffset.UtcNow,
        };
        var own = new AuditEntry
        {
            Id = Guid.CreateVersion7(), ClubId = world.A.ClubId, Scope = AuditScope.Club, Action = AuditAction.RoleAssigned,
            TargetType = AuditTarget.Membership, TargetId = world.A.Iara.MembershipId, OccurredAt = DateTimeOffset.UtcNow,
        };
        app.AuditEntries.AddRange(platform, own);
        Assert.Equal(2, await app.SaveChangesAsync());
        Assert.Equal(2L, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM audit_entries WHERE id IN (@platform, @own)", ("platform", platform.Id), ("own", own.Id)));
    }

    // O guarda também cobre o aviso, o registro de fundação e o próprio clube: com o contexto em A, o que é de B é barrado e o
    // que é de A é gravado. Papel dono, sem RLS, para ver só a primeira barreira (RN-TEN-001, D-124).
    [Fact]
    public async Task CA_TEN_001_Write_guard_checks_notifications_founding_records_and_clubs()
    {
        var world = await CreateWorldAsync();
        var context = new ClubContext { ClubId = world.A.ClubId, PersonId = world.A.Aurora.PersonId };
        await using var owner = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.OwnerConnectionString).AddInterceptors(new ClubWriteGuardInterceptor(context)).Options, context);
        Notification NotificationIn(Guid club) => new()
        {
            Id = Guid.CreateVersion7(), RecipientPersonId = world.A.Iara.PersonId, Event = NotificationEvent.JoinQueued,
            ClubId = club, ObjectId = Guid.CreateVersion7(), CreatedAt = DateTimeOffset.UtcNow,
        };
        FoundingRecord RecordIn(Guid club) => new()
        {
            Id = Guid.CreateVersion7(), PersonId = world.Requester, ClubId = club, ApprovedAt = DateTimeOffset.UtcNow,
        };

        owner.Notifications.Add(NotificationIn(world.B.ClubId));
        await Assert.ThrowsAsync<ClubIsolationException>(() => owner.SaveChangesAsync());
        owner.ChangeTracker.Clear();
        owner.FoundingRecords.Add(RecordIn(world.B.ClubId));
        await Assert.ThrowsAsync<ClubIsolationException>(() => owner.SaveChangesAsync());
        owner.ChangeTracker.Clear();
        (await owner.Clubs.IgnoreQueryFilters().SingleAsync(c => c.Id == world.B.ClubId)).Name = "Clube Alterado";
        await Assert.ThrowsAsync<ClubIsolationException>(() => owner.SaveChangesAsync());
        owner.ChangeTracker.Clear();

        owner.Notifications.Add(NotificationIn(world.A.ClubId));
        owner.FoundingRecords.Add(RecordIn(world.A.ClubId));
        (await owner.Clubs.SingleAsync(c => c.Id == world.A.ClubId)).Name = "Clube Renomeado";
        Assert.Equal(3, await owner.SaveChangesAsync());
        Assert.Equal("Clube Corujas", await OwnerSql.ScalarAsync<string>(db.OwnerConnectionString,
            "SELECT name FROM clubs WHERE id = @b", ("b", world.B.ClubId)));
    }

    // 100 escopos alternando A e B pelo mesmo pool de 4 conexões nunca misturam o clube (CA-TEN-001 g, RSK-TEN-001).
    [Fact]
    public async Task CA_TEN_001_R1_concurrent_scopes_never_mix_clubs()
    {
        var world = await CreateWorldAsync();
        await using var services = DbScope.Create(db, maxPoolSize: 4);

        var results = await Task.WhenAll(Enumerable.Range(0, 100).Select(async i =>
        {
            var club = i % 2 == 0 ? world.A.ClubId : world.B.ClubId;
            await using var scope = services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ClubContext>().ClubId = club;
            var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clubs = await app.RoleAssignments.IgnoreQueryFilters().Select(r => r.ClubId).Distinct().ToListAsync();
            return clubs.SequenceEqual([club]);
        }));

        Assert.All(results, ok => Assert.True(ok));
    }

    // O papel de admin do contexto chega ao banco quando a conexão abre, e um escopo sem o papel, na mesma conexão física do pool,
    // não o vê (CA-TEN-001, RNF-TEN-006).
    [Fact]
    public async Task CA_TEN_001_R1_session_carries_platform_admin_only_for_admin_scopes()
    {
        var world = await CreateWorldAsync();
        await using var services = DbScope.Create(db, maxPoolSize: 1);

        async Task<int> OverviewRowsAsync(bool admin)
        {
            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ClubContext>();
            context.PersonId = world.Admin.PersonId;
            context.IsPlatformAdmin = admin;
            var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await app.AdminClubOverview.IgnoreQueryFilters().CountAsync();
        }

        Assert.True(await OverviewRowsAsync(admin: true) > 0);
        Assert.Equal(0, await OverviewRowsAsync(admin: false));
        Assert.True(await OverviewRowsAsync(admin: true) > 0);
    }
}
