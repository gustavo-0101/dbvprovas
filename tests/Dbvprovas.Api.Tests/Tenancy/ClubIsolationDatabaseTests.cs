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
    public async Task CA_TEN_001_Clubs_are_filtered_by_ef_and_rls()
    {
        var context = new ClubContext { ClubId = DevSeed.ClubA, PersonId = DevSeed.AuroraPerson };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db.OwnerConnectionString).Options;
        await using var owner = new AppDbContext(options, context);
        Assert.Equal([DevSeed.ClubA], await owner.Clubs.Select(club => club.Id).ToArrayAsync());
        context.ClubId = null;
        context.PersonId = null;
        Assert.Empty(await owner.Clubs.ToListAsync());
        context.PersonId = DevSeed.AuroraPerson;
        Assert.Equal([DevSeed.ClubA], await owner.Clubs.Select(club => club.Id).ToArrayAsync());

        await using var services = DbScope.Create(db);
        await using var scope = services.CreateAsyncScope();
        var app = InClubA(scope);
        Assert.Equal([DevSeed.ClubA], await app.Clubs.IgnoreQueryFilters().Select(club => club.Id).ToArrayAsync());
        var appContext = scope.ServiceProvider.GetRequiredService<ClubContext>();
        appContext.ClubId = null;
        appContext.PersonId = null;
        Assert.Empty(await app.Clubs.IgnoreQueryFilters().ToListAsync());
        appContext.PersonId = DevSeed.AuroraPerson;
        Assert.Equal([DevSeed.ClubA], await app.Clubs.IgnoreQueryFilters().Select(club => club.Id).ToArrayAsync());
    }

    [Fact]
    public async Task CA_TEN_001_Club_bootstrap_reads_only_own_active_memberships()
    {
        var member = await TestData.CreateClubWithMemberAsync(db, "Synthetic Member");
        await using var services = DbScope.Create(db);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubContext>();
        context.PersonId = member.PersonId;
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db.OwnerConnectionString).Options;
        await using var owner = new AppDbContext(options, context);
        Assert.Equal([member.ClubId], await owner.Clubs.Select(club => club.Id).ToArrayAsync());
        Assert.Equal([member.ClubId], await app.Clubs.IgnoreQueryFilters().Select(club => club.Id).ToArrayAsync());
        await TestData.EndMembershipAsync(db, member.MembershipId);
        Assert.Empty(await owner.Clubs.ToListAsync());
        Assert.Empty(await app.Clubs.IgnoreQueryFilters().ToListAsync());
    }

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
