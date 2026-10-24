using Dbvprovas.Api.Infrastructure;

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
            "accounts:INSERT", "accounts:UPDATE",
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

    // As funções que atravessam o RLS são SECURITY DEFINER com search_path fixo, executáveis pelo app e nunca por PUBLIC
    // (RNF-TEN-006). ACL nula é o padrão do Postgres, que dá EXECUTE a PUBLIC: conta como falha.
    [Fact]
    public async Task Security_definer_functions_have_fixed_search_path_and_no_public_execute()
    {
        foreach (var function in new[] { "club_id_by_invite", "founding_requester_state", "join_request_clubs_due",
                     "account_by_google_subject", "account_email", "outbox_recipient_email", "adult_platform_admin_ids" })
        {
            Assert.True(await OwnerSql.ScalarAsync<bool>(db.OwnerConnectionString,
                "SELECT prosecdef AND proconfig @> ARRAY['search_path=pg_catalog, public'] FROM pg_proc WHERE proname = @f", ("f", function)), function);
            Assert.True(await OwnerSql.ScalarAsync<bool>(db.OwnerConnectionString, """
                SELECT p.proacl IS NOT NULL
                   AND NOT has_function_privilege('public', p.oid, 'EXECUTE')
                   AND has_function_privilege('dbv_app', p.oid, 'EXECUTE')
                FROM pg_proc p WHERE p.proname = @f
                """, ("f", function)), function);
        }
    }

    // E-mail e sub do Google são dado pessoal da conta: o app não os lê direto, só pelas funções que conferem quem pede
    // (RN-PRV-001, RN-ID-001).
    [Fact]
    public async Task Personal_account_columns_are_closed_to_the_app()
    {
        await using var connection = new Npgsql.NpgsqlConnection(db.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand("""
            SELECT column_name FROM information_schema.column_privileges
            WHERE grantee = 'dbv_app' AND table_schema = 'public' AND table_name = 'accounts' AND privilege_type = 'SELECT'
            """, connection);
        var columns = new SortedSet<string>(StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(0));

        Assert.Equal(new SortedSet<string>(StringComparer.Ordinal) { "created_at", "id", "is_platform_admin", "person_id" }, columns);
    }

    // Unicidades do R1 no banco (RN-TEN-006, D-170, D-163, RN-JOIN-006, D-162, RN-ID-001).
    [Fact]
    public async Task Schema_has_the_r1_unique_indexes()
    {
        foreach (var index in DbConstraints.All)
            Assert.True(await OwnerSql.ScalarAsync<bool>(db.OwnerConnectionString,
                "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = @i)", ("i", index)), index);
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
