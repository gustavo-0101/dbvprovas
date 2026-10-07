namespace Dbvprovas.Api.Tests.Infrastructure;

public sealed record TestMember(Guid ClubId, Guid PersonId, Guid AccountId, Guid MembershipId);

// Dados próprios de cada teste, sem alterar o seed compartilhado (RF-TEN-003).
public static class TestData
{
    public static async Task<TestMember> CreateClubWithMemberAsync(PostgresFixture db, string personName)
    {
        var member = new TestMember(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
            INSERT INTO clubs (id, name, status) VALUES (@club, 'Clube de Teste', 'Active');
            INSERT INTO persons (id, name) VALUES (@person, @name);
            INSERT INTO accounts (id, person_id) VALUES (@account, @person);
            INSERT INTO memberships (id, club_id, person_id, started_at) VALUES (@membership, @club, @person, now());
            """,
            ("club", member.ClubId), ("person", member.PersonId), ("name", personName),
            ("account", member.AccountId), ("membership", member.MembershipId));
        return member;
    }

    public static Task EndMembershipAsync(PostgresFixture db, Guid membershipId) =>
        OwnerSql.ExecuteAsync(db.OwnerConnectionString, "UPDATE memberships SET ended_at = now() WHERE id = @id", ("id", membershipId));
}
