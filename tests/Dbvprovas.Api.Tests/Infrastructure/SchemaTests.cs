using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Tests.Infrastructure;

public sealed class SchemaTests(PostgresFixture db)
{
    // Segunda barreira do isolamento: RLS ligado e app sem como ignorá-lo (RNF-TEN-001, D-121).
    [Fact]
    public async Task Schema_enables_rls_and_app_role_cannot_bypass_it()
    {
        var rls = await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM pg_class WHERE relname IN ('clubs', 'memberships', 'persons') AND relrowsecurity");
        var bypass = await OwnerSql.ScalarAsync<bool>(db.OwnerConnectionString,
            "SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = 'dbv_app'");
        var owner = await OwnerSql.ScalarAsync<string>(db.OwnerConnectionString,
            "SELECT tableowner::text FROM pg_tables WHERE tablename = 'memberships'");

        Assert.Equal(3, rls);
        Assert.False(bypass);
        Assert.Equal("dbv_owner", owner);
    }

    // Seed fictício com dois clubes e três membros em cada (RF-TEN-003).
    [Fact]
    public async Task Dev_seed_creates_two_fictitious_clubs()
    {
        var a = await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM memberships WHERE club_id = @club", ("club", DevSeed.ClubA));
        var b = await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM memberships WHERE club_id = @club", ("club", DevSeed.ClubB));

        Assert.Equal(3, a);
        Assert.Equal(3, b);
    }

    private static readonly string[] RlsTables =
    [
        "clubs", "memberships", "persons", "units", "role_assignments", "director_nominations", "join_requests",
        "consents", "founding_requests", "founding_records", "admin_access_requests", "audit_entries", "notifications",
    ];

    // Segunda barreira em toda tabela de clube ou de dado pessoal do R1 (PC-01, RNF-TEN-006). Contas, sessões e fila de
    // e-mail não pertencem a clube e ficam sem RLS; o e-mail e o sub da conta ficam fechados ao app (teste abaixo).
    [Fact]
    public async Task CA_TEN_001_Every_r1_club_or_personal_table_has_rls()
    {
        foreach (var table in RlsTables)
            Assert.True(await OwnerSql.ScalarAsync<bool>(db.OwnerConnectionString,
                "SELECT relrowsecurity FROM pg_class WHERE relname = @t AND relkind = 'r'", ("t", table)), table);
        foreach (var table in new[] { "accounts", "email_outbox", "sessions" })
            Assert.False(await OwnerSql.ScalarAsync<bool>(db.OwnerConnectionString,
                "SELECT relrowsecurity FROM pg_class WHERE relname = @t AND relkind = 'r'", ("t", table)), table);
    }

    // O papel do app recebe exatamente o que o R1 usa (D-121): sem DELETE fora da limpeza de pedidos de entrada e de
    // sessões, e auditoria só com SELECT e INSERT.
    [Fact]
    public async Task Schema_grants_app_role_exactly_the_r1_privileges()
    {
        var expected = new SortedSet<string>(StringComparer.Ordinal)
        {
            "accounts:INSERT",
            "admin_access_requests:INSERT", "admin_access_requests:SELECT", "admin_access_requests:UPDATE",
            "admin_club_overview:SELECT", "audit_entries:INSERT", "audit_entries:SELECT",
            "club_directory:SELECT", "clubs:INSERT", "clubs:SELECT", "clubs:UPDATE",
            "consents:INSERT", "consents:SELECT", "consents:UPDATE",
            "director_nominations:INSERT", "director_nominations:SELECT", "director_nominations:UPDATE",
            "email_outbox:INSERT", "email_outbox:SELECT", "email_outbox:UPDATE",
            "founding_records:INSERT", "founding_records:SELECT",
            "founding_requests:INSERT", "founding_requests:SELECT", "founding_requests:UPDATE",
            "join_requests:DELETE", "join_requests:INSERT", "join_requests:SELECT", "join_requests:UPDATE",
            "memberships:INSERT", "memberships:SELECT", "memberships:UPDATE",
            "notifications:INSERT", "notifications:SELECT", "notifications:UPDATE",
            "persons:INSERT", "persons:SELECT", "persons:UPDATE",
            "role_assignments:INSERT", "role_assignments:SELECT", "role_assignments:UPDATE",
            "sessions:DELETE", "sessions:INSERT", "sessions:SELECT", "sessions:UPDATE",
            "units:INSERT", "units:SELECT", "units:UPDATE",
        };
        await using var connection = new Npgsql.NpgsqlConnection(db.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            "SELECT table_name || ':' || privilege_type FROM information_schema.role_table_grants WHERE grantee = 'dbv_app' AND table_schema = 'public'", connection);
        var actual = new SortedSet<string>(StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                actual.Add(reader.GetString(0));

        Assert.Equal(expected, actual);
    }

    // As funções que atravessam o RLS são SECURITY DEFINER com search_path fixo, com pg_temp por último para uma tabela
    // temporária não fazer sombra, executáveis pelo app e nunca por PUBLIC (RNF-TEN-006). ACL nula é o padrão do
    // Postgres, que dá EXECUTE a PUBLIC: conta como falha.
    [Fact]
    public async Task Security_definer_functions_have_fixed_search_path_and_no_public_execute()
    {
        foreach (var function in new[] { "club_id_by_invite", "founding_requester_state", "join_request_clubs_due",
                     "account_by_google_subject", "account_email", "outbox_recipient_email", "adult_platform_admin_ids" })
        {
            Assert.True(await OwnerSql.ScalarAsync<bool>(db.OwnerConnectionString,
                "SELECT prosecdef AND proconfig @> ARRAY['search_path=pg_catalog, public, pg_temp'] FROM pg_proc WHERE proname = @f", ("f", function)), function);
            Assert.True(await OwnerSql.ScalarAsync<bool>(db.OwnerConnectionString, """
                SELECT p.proacl IS NOT NULL
                   AND NOT has_function_privilege('public', p.oid, 'EXECUTE')
                   AND has_function_privilege('dbv_app', p.oid, 'EXECUTE')
                FROM pg_proc p WHERE p.proname = @f
                """, ("f", function)), function);
        }
    }

    // E-mail e sub do Google são dado pessoal da conta: o app não os lê direto, só pelas funções que conferem quem pede
    // (RN-PRV-001, RN-ID-001). Na conta, o app só atualiza o e-mail: o sub e o papel de admin não mudam por ele (RN-AUD-005).
    [Fact]
    public async Task Personal_account_columns_are_closed_to_the_app()
    {
        Assert.Equal(new SortedSet<string>(StringComparer.Ordinal) { "created_at", "id", "is_platform_admin", "person_id" },
            await AppAccountColumnsAsync("SELECT"));
        Assert.Equal(new SortedSet<string>(StringComparer.Ordinal) { "email" }, await AppAccountColumnsAsync("UPDATE"));
    }

    // Unicidades do R1 no banco (RN-TEN-006, D-170, D-163, RN-JOIN-006, D-162, RN-ID-001): cada índice existe e é único.
    [Fact]
    public async Task Schema_has_the_r1_unique_indexes()
    {
        foreach (var index in DbConstraints.All)
            Assert.True(await OwnerSql.ScalarAsync<bool>(db.OwnerConnectionString, """
                SELECT EXISTS (SELECT 1 FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid
                               WHERE c.relname = @i AND i.indisunique)
                """, ("i", index)), index);
    }

    // O admin, no contexto de um clube, não muda o consentimento de um membro: só o pré-fundação, ao dar a ele o clube
    // aprovado (CA-PRV-008, CA-TEN-010).
    [Fact]
    public async Task CA_PRV_008_Admin_updates_only_pre_founding_consents()
    {
        var club = await TestData.CreateClubAsync(db);
        var member = await TestData.CreateMemberAsync(db, club, "Membro Teste");
        var admin = await TestData.CreateAdminAsync(db);
        var requester = await TestData.CreatePersonAsync(db, "Solicitante Teste");
        var request = await CreateFoundingRequestAsync(requester, club, "Approved");
        var memberConsent = await OwnerSql.ScalarAsync<Guid>(db.OwnerConnectionString,
            "SELECT id FROM consents WHERE person_id = @p AND club_id = @c", ("p", member.PersonId), ("c", club));
        var preFoundingConsent = Guid.CreateVersion7();
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
            INSERT INTO consents (id, person_id, kind, founding_request_id, term_version, consented_by_account_id, granted_at)
                VALUES (@id, @person, 'OwnParticipation', @request, @version, @account, now())
            """,
            ("id", preFoundingConsent), ("person", requester.PersonId), ("request", request),
            ("version", ConsentTerms.AdultOwnParticipationVersion), ("account", requester.AccountId));

        var revoke = await AppErrorAsync(club, admin.PersonId, platformAdmin: true,
            "UPDATE consents SET revoked_at = now() WHERE id = @id", ("id", memberConsent));
        var moved = await AppExecuteAsync(club, admin.PersonId, platformAdmin: true,
            "UPDATE consents SET club_id = @club WHERE id = @id", ("club", club), ("id", preFoundingConsent));

        Assert.Equal(Npgsql.PostgresErrorCodes.InsufficientPrivilege, revoke);
        Assert.True(await OwnerSql.ScalarAsync<bool>(db.OwnerConnectionString,
            "SELECT revoked_at IS NULL FROM consents WHERE id = @id", ("id", memberConsent)));
        Assert.Equal(1, moved);
        Assert.Equal(club, await OwnerSql.ScalarAsync<Guid>(db.OwnerConnectionString,
            "SELECT club_id FROM consents WHERE id = @id", ("id", preFoundingConsent)));
    }

    // Na conta, o app só troca o e-mail; o papel de admin e o sub ficam fora do alcance dele (RN-PRV-001, RN-AUD-005, D-121).
    [Fact]
    public async Task App_updates_only_the_email_of_an_account()
    {
        var person = await TestData.CreatePersonAsync(db, "Conta Teste");
        var newEmail = $"n{person.PersonId:N}@example.com";

        var admin = await AppErrorAsync(null, person.PersonId, platformAdmin: false,
            "UPDATE accounts SET is_platform_admin = true WHERE id = @id", ("id", person.AccountId));
        var subject = await AppErrorAsync(null, person.PersonId, platformAdmin: false,
            "UPDATE accounts SET google_subject = @s WHERE id = @id", ("s", $"other-{person.PersonId:N}"), ("id", person.AccountId));
        var email = await AppExecuteAsync(null, person.PersonId, platformAdmin: false,
            "UPDATE accounts SET email = @e WHERE id = @id", ("e", newEmail), ("id", person.AccountId));

        Assert.Equal(Npgsql.PostgresErrorCodes.InsufficientPrivilege, admin);
        Assert.Equal(Npgsql.PostgresErrorCodes.InsufficientPrivilege, subject);
        Assert.Equal(1, email);
        Assert.Equal(newEmail, await OwnerSql.ScalarAsync<string>(db.OwnerConnectionString,
            "SELECT email FROM accounts WHERE id = @id", ("id", person.AccountId)));
        Assert.False(await OwnerSql.ScalarAsync<bool>(db.OwnerConnectionString,
            "SELECT is_platform_admin FROM accounts WHERE id = @id", ("id", person.AccountId)));
    }

    // Clube só nasce pela aprovação da fundação, com o papel de admin (CA-TEN-001, CA-TEN-010, D-121).
    [Fact]
    public async Task CA_TEN_001_Club_insert_requires_platform_admin()
    {
        var person = await TestData.CreatePersonAsync(db, "Pessoa Teste");
        var refused = Guid.CreateVersion7();
        var created = Guid.CreateVersion7();
        const string insert = """
            INSERT INTO clubs (id, name, city, invite_code, status, created_at)
                VALUES (@id, 'Clube Novo', 'Cidade Teste', @invite, 'Active', now())
            """;

        var withoutAdmin = await AppErrorAsync(refused, person.PersonId, platformAdmin: false, insert,
            ("id", refused), ("invite", InviteCodes.New()));
        var withAdmin = await AppExecuteAsync(created, person.PersonId, platformAdmin: true, insert,
            ("id", created), ("invite", InviteCodes.New()));

        Assert.Equal(Npgsql.PostgresErrorCodes.InsufficientPrivilege, withoutAdmin);
        Assert.Equal(0L, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM clubs WHERE id = @id", ("id", refused)));
        Assert.Equal(1, withAdmin);
    }

    // O solicitante só cancela o próprio pedido de fundação ainda pendente; o decidido fica como está (CA-TEN-009, CA-TEN-010).
    [Fact]
    public async Task CA_TEN_009_Requester_cancels_only_a_pending_founding_request()
    {
        var requester = await TestData.CreatePersonAsync(db, "Solicitante Teste");
        var approved = await CreateFoundingRequestAsync(requester, Guid.CreateVersion7(), "Approved");
        var pending = await CreateFoundingRequestAsync(requester, Guid.CreateVersion7(), "Pending");
        const string cancel = "UPDATE founding_requests SET status = 'Cancelled', closed_at = now() WHERE id = @id";

        var decided = await AppExecuteAsync(null, requester.PersonId, platformAdmin: false, cancel, ("id", approved));
        var own = await AppExecuteAsync(null, requester.PersonId, platformAdmin: false, cancel, ("id", pending));

        Assert.Equal(0, decided);
        Assert.Equal("Approved", await OwnerSql.ScalarAsync<string>(db.OwnerConnectionString,
            "SELECT status FROM founding_requests WHERE id = @id", ("id", approved)));
        Assert.Equal(1, own);
    }

    // A visão do admin também tem a primeira barreira: sem o papel de admin no contexto, o EF não traz nada, mesmo com a
    // sessão do banco marcada como admin (RNF-TEN-001, D-148).
    [Fact]
    public async Task Admin_club_overview_is_hidden_by_ef_without_platform_admin()
    {
        var club = await TestData.CreateClubAsync(db);
        var context = new ClubContext();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db.OwnerConnectionString).Options;
        await using var owner = new AppDbContext(options, context);
        await owner.Database.OpenConnectionAsync();
        await owner.Database.ExecuteSqlRawAsync("SELECT set_config('app.platform_admin', 'true', false)");

        var hidden = await owner.AdminClubOverview.Where(o => o.Id == club).ToListAsync();
        context.IsPlatformAdmin = true;
        var shown = await owner.AdminClubOverview.Where(o => o.Id == club).ToListAsync();

        Assert.Empty(hidden);
        Assert.Single(shown);
    }

    private async Task<SortedSet<string>> AppAccountColumnsAsync(string privilege)
    {
        await using var connection = new Npgsql.NpgsqlConnection(db.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand("""
            SELECT column_name FROM information_schema.column_privileges
            WHERE grantee = 'dbv_app' AND table_schema = 'public' AND table_name = 'accounts' AND privilege_type = @privilege
            """, connection);
        command.Parameters.AddWithValue("privilege", privilege);
        var columns = new SortedSet<string>(StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(0));
        return columns;
    }

    private async Task<Guid> CreateFoundingRequestAsync(TestPerson requester, Guid proposedClubId, string status)
    {
        var request = Guid.CreateVersion7();
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
            INSERT INTO founding_requests (id, requester_person_id, requester_account_id, club_name, club_city, proposed_club_id,
                    status, exception_requested, exception_granted, created_at, closed_at)
                VALUES (@id, @person, @account, 'Clube Proposto', 'Cidade Teste', @club, @status, false, false, now(),
                        CASE WHEN @status = 'Pending' THEN NULL ELSE now() END)
            """,
            ("id", request), ("person", requester.PersonId), ("account", requester.AccountId), ("club", proposedClubId), ("status", status));
        return request;
    }

    // Comando pela conexão do app, com o contexto gravado na sessão como o interceptador grava (D-121).
    private async Task<int> AppExecuteAsync(Guid? club, Guid? person, bool platformAdmin, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new Npgsql.NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();
        await using (var context = new Npgsql.NpgsqlCommand(
            "SELECT set_config('app.club_id', @club, false), set_config('app.person_id', @person, false), set_config('app.platform_admin', @admin, false)",
            connection))
        {
            context.Parameters.AddWithValue("club", club?.ToString() ?? string.Empty);
            context.Parameters.AddWithValue("person", person?.ToString() ?? string.Empty);
            context.Parameters.AddWithValue("admin", platformAdmin ? "true" : "false");
            await context.ExecuteNonQueryAsync();
        }

        await using var command = new Npgsql.NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return await command.ExecuteNonQueryAsync();
    }

    private async Task<string> AppErrorAsync(Guid? club, Guid? person, bool platformAdmin, string sql, params (string Name, object Value)[] parameters)
    {
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => AppExecuteAsync(club, person, platformAdmin, sql, parameters));
        return error.SqlState;
    }

    // Seed fictício do R1: diretores, secretários, tesoureira, consentimentos e o admin (RF-TEN-003, PC-04).
    [Fact]
    public async Task Dev_seed_has_r1_roles_consents_and_platform_admin()
    {
        var directors = await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM role_assignments WHERE role = 'Director' AND ended_at IS NULL AND club_id IN (@a, @b)",
            ("a", DevSeed.ClubA), ("b", DevSeed.ClubB));
        var consents = await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM consents WHERE club_id IN (@a, @b) AND revoked_at IS NULL", ("a", DevSeed.ClubA), ("b", DevSeed.ClubB));
        var admin = await OwnerSql.ScalarAsync<bool>(db.OwnerConnectionString,
            "SELECT is_platform_admin FROM accounts WHERE id = @id", ("id", DevSeed.AdminAlfaAccount));

        Assert.Equal(2, directors);
        Assert.Equal(6, consents);
        Assert.True(admin);
    }
}
