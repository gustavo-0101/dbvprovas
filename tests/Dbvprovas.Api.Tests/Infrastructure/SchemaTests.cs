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
}
