using Dbvprovas.Api.Modules.Organization;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Dbvprovas.Api.Tests.Shared;

public sealed class ActorServiceTests(PostgresFixture db)
{
    private async Task<ClubActor?> ActorAsync(Guid clubId, Guid personId)
    {
        await using var services = DbScope.Create(db);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ClubContext>();
        context.ClubId = clubId;
        context.PersonId = personId;
        return await scope.ServiceProvider.GetRequiredService<ActorService>().GetClubActorAsync(CancellationToken.None);
    }

    // PC-06, PC-09: cargos vigentes, idade na data e diretoria só com 18+, relidos a cada requisição.
    [Fact]
    public async Task Actor_has_active_roles_age_and_board_flags()
    {
        var a = await TestData.CreateClubAAsync(db);

        var aurora = await ActorAsync(a.ClubId, a.Aurora.PersonId);
        var lina = await ActorAsync(a.ClubId, a.Lina.PersonId);
        var iara = await ActorAsync(a.ClubId, a.Iara.PersonId);

        Assert.NotNull(aurora);
        Assert.True(aurora.IsDirector);
        Assert.True(aurora.IsBoard);
        Assert.Equal(38, aurora.Age);
        Assert.NotNull(lina);
        Assert.False(lina.IsBoard);
        Assert.Equal(a.UnitX, Assert.Single(lina.Roles).UnitId);
        Assert.NotNull(iara);
        Assert.False(iara.HasAnyRole);
    }

    // PC-06: sem nascimento, a pessoa nunca conta como adulta nem como diretoria.
    [Fact]
    public async Task Actor_without_birth_date_is_never_adult()
    {
        var club = await TestData.CreateClubAsync(db);
        var member = await TestData.CreateMemberAsync(db, club, "Sem Data", age: null, role: RoleCode.Secretary);

        var actor = await ActorAsync(club, member.PersonId);

        Assert.NotNull(actor);
        Assert.Null(actor.Age);
        Assert.False(actor.IsAdult);
        Assert.False(actor.IsBoard);
        Assert.False(actor.IsDirectorOrSecretary);
    }

    // PC-06: cargo de diretoria com 17 anos não dá poder de diretoria.
    [Fact]
    public async Task Board_role_under_18_does_not_count_as_board()
    {
        var club = await TestData.CreateClubAsync(db);
        var member = await TestData.CreateMemberAsync(db, club, "Dezessete", age: 17, role: RoleCode.Treasurer);

        var actor = await ActorAsync(club, member.PersonId);

        Assert.NotNull(actor);
        Assert.True(actor.Has(RoleCode.Treasurer));
        Assert.False(actor.IsBoard);
    }

    // CA-TEN-008, D-155: participação suspensa ou encerrada não age; cargo encerrado não conta.
    [Fact]
    public async Task Only_active_membership_and_active_roles_count()
    {
        var club = await TestData.CreateClubAsync(db);
        var suspended = await TestData.AddMemberAsync(db, club, await TestData.CreatePersonAsync(db, "Suspensa"), MembershipStatus.Suspended);
        var active = await TestData.CreateMemberAsync(db, club, "Ativa", role: RoleCode.Chaplain);
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString,
            "UPDATE role_assignments SET ended_at = now(), end_cause = 'RemovedByBoard' WHERE membership_id = @m", ("m", active.MembershipId));

        Assert.Null(await ActorAsync(club, suspended.PersonId));
        var actor = await ActorAsync(club, active.PersonId);
        Assert.NotNull(actor);
        Assert.False(actor.HasAnyRole);
    }
}
