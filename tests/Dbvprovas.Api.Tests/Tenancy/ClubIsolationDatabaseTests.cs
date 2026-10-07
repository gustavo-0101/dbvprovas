using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dbvprovas.Api.Tests.Tenancy;

public sealed class ClubIsolationDatabaseTests(PostgresFixture db)
{
    [Fact]
    public async Task CA_TEN_001_Database_hides_other_club_with_filter_off()
    {
        await using var services = DbScope.Create(db);
        await using var scope = services.CreateAsyncScope();
        var app = InClubA(scope);

        var otherClub = await app.Memberships.IgnoreQueryFilters().CountAsync(m => m.ClubId == DevSeed.ClubB);
        var names = await app.Persons.IgnoreQueryFilters().Select(p => p.Name).ToListAsync();

        Assert.Equal(0, otherClub);
        Assert.Equal(DevSeed.ClubAMemberNames.Order(), names.Order());
    }

    [Fact]
    public async Task CA_TEN_001_Write_guard_rejects_entities_of_other_club()
    {
        await using var services = DbScope.Create(db);
        await using var scope = services.CreateAsyncScope();
        var app = InClubA(scope);
        app.Memberships.Add(NewMembership(DevSeed.ClubB));

        await Assert.ThrowsAsync<ClubIsolationException>(() => app.SaveChangesAsync());
    }

    [Fact]
    public async Task CA_TEN_001_Database_rejects_writes_into_other_club()
    {
        await using var services = DbScope.Create(db);
        await using var scope = services.CreateAsyncScope();
        var app = InClubA(scope);

        var insert = await Assert.ThrowsAsync<PostgresException>(() => app.Database.ExecuteSqlAsync(
            $"INSERT INTO memberships (id, club_id, person_id, started_at) VALUES ({Guid.CreateVersion7()}, {DevSeed.ClubB}, {DevSeed.AuroraPerson}, now())"));
        var move = await Assert.ThrowsAsync<PostgresException>(() => app.Database.ExecuteSqlAsync(
            $"UPDATE memberships SET club_id = {DevSeed.ClubB} WHERE id = {DevSeed.CaioMembership}"));
        var touched = await app.Database.ExecuteSqlAsync(
            $"UPDATE memberships SET ended_at = now() WHERE id = {DevSeed.ElisaMembership}");

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, insert.SqlState);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, move.SqlState);
        Assert.Equal(0, touched);
    }

    [Fact]
    public async Task CA_TEN_001_Without_club_context_reads_nothing()
    {
        await using var services = DbScope.Create(db);
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(0, await app.Memberships.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await app.Persons.IgnoreQueryFilters().CountAsync());

        app.Memberships.Add(NewMembership(DevSeed.ClubA));
        await Assert.ThrowsAsync<ClubIsolationException>(() => app.SaveChangesAsync());
    }

    private static AppDbContext InClubA(AsyncServiceScope scope)
    {
        var context = scope.ServiceProvider.GetRequiredService<ClubContext>();
        context.ClubId = DevSeed.ClubA;
        context.PersonId = DevSeed.AuroraPerson;
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    private static Membership NewMembership(Guid clubId) => new()
    {
        Id = Guid.CreateVersion7(),
        ClubId = clubId,
        PersonId = DevSeed.AuroraPerson,
        StartedAt = TimeProvider.System.GetUtcNow(),
    };
}
