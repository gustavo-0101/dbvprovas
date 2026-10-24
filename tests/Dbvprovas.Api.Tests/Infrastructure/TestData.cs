using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Infrastructure;

public sealed record TestMember(Guid ClubId, Guid PersonId, Guid AccountId, Guid MembershipId);

public sealed record TestPerson(Guid PersonId, Guid AccountId, string Name);

public sealed record TestActor(Guid ClubId, Guid PersonId, Guid AccountId, Guid MembershipId, string Name);

// Personas fictícias do R1, com IDs novos a cada teste: Aurora diretora, Caio secretário, Dalva tesoureira,
// Iara sem cargo e Lina conselheira da unidade X (feminina); a unidade Y é masculina.
public sealed record TestClubA(Guid ClubId, Guid UnitX, Guid UnitY, TestActor Aurora, TestActor Caio, TestActor Dalva, TestActor Iara, TestActor Lina);

public sealed record TestClubB(Guid ClubId, Guid UnitZ, TestActor Bento);

// Dados próprios de cada teste, como dono das tabelas, sem tocar no seed compartilhado (RF-TEN-003, PC-04).
public static class TestData
{
    // Data de nascimento de quem tem `age` anos hoje em Brasília, longe do aniversário.
    public static DateOnly BirthDateForAge(int age, TimeProvider? clock = null) =>
        BrasiliaCalendar.Today(clock ?? TimeProvider.System).AddYears(-age).AddDays(-30);

    // email, subject e birthDate fixam valores sentinela para os testes de vazamento (CA-AUD-004); sem eles, valores gerados.
    public static async Task<TestPerson> CreatePersonAsync(PostgresFixture db, string name, int? age = 30, Sex sex = Sex.Female,
        bool isPlatformAdmin = false, TimeProvider? clock = null, string? email = null, string? subject = null, DateOnly? birthDate = null)
    {
        var person = new TestPerson(Guid.CreateVersion7(), Guid.CreateVersion7(), name);
        object birth = birthDate is { } fixedBirth ? fixedBirth : age is { } years ? BirthDateForAge(years, clock) : DBNull.Value;
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
            INSERT INTO persons (id, name, birth_date, sex, created_at) VALUES (@person, @name, @birth, @sex, now());
            INSERT INTO accounts (id, person_id, google_subject, email, is_platform_admin, created_at)
                VALUES (@account, @person, @subject, @email, @admin, now());
            """,
            ("person", person.PersonId), ("name", name), ("birth", birth), ("sex", sex.ToString()),
            ("account", person.AccountId), ("subject", subject ?? $"test-{person.PersonId:N}"), ("email", email ?? $"p{person.PersonId:N}@example.com"),
            ("admin", isPlatformAdmin));
        return person;
    }

    public static Task<TestPerson> CreateAdminAsync(PostgresFixture db, string name = "Admin Alfa", int age = 36, TimeProvider? clock = null) =>
        CreatePersonAsync(db, name, age, Sex.Female, isPlatformAdmin: true, clock: clock);

    public static async Task<Guid> CreateClubAsync(PostgresFixture db, string name = "Clube de Teste", string city = "Cidade Teste")
    {
        var club = Guid.CreateVersion7();
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString,
            "INSERT INTO clubs (id, name, city, invite_code, status, created_at) VALUES (@club, @name, @city, @invite, 'Active', now())",
            ("club", club), ("name", name), ("city", city), ("invite", InviteCodes.New()));
        return club;
    }

    public static async Task<Guid> CreateUnitAsync(PostgresFixture db, Guid clubId, string name, Sex sex, int minAge = 10, int maxAge = 15)
    {
        var unit = Guid.CreateVersion7();
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
            INSERT INTO units (id, club_id, name, sex, min_age, max_age, status, created_at)
                VALUES (@unit, @club, @name, @sex, @min, @max, 'Active', now())
            """,
            ("unit", unit), ("club", clubId), ("name", name), ("sex", sex.ToString()), ("min", minAge), ("max", maxAge));
        return unit;
    }

    public static async Task<TestActor> AddMemberAsync(PostgresFixture db, Guid clubId, TestPerson person,
        MembershipStatus status = MembershipStatus.Active, bool withConsent = true)
    {
        var membership = Guid.CreateVersion7();
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
            INSERT INTO memberships (id, club_id, person_id, kind, status, started_at, suspended_at, ended_at)
                VALUES (@membership, @club, @person, 'Leader', @status, now(),
                        CASE WHEN @status = 'Suspended' THEN now() END,
                        CASE WHEN @status = 'Ended' THEN now() END)
            """,
            ("membership", membership), ("club", clubId), ("person", person.PersonId), ("status", status.ToString()));
        if (withConsent)
            await OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
                INSERT INTO consents (id, person_id, kind, club_id, term_version, consented_by_account_id, granted_at)
                    VALUES (@id, @person, 'OwnParticipation', @club, @version, @account, now())
                """,
                ("id", Guid.CreateVersion7()), ("person", person.PersonId), ("club", clubId),
                ("version", ConsentTerms.AdultOwnParticipationVersion), ("account", person.AccountId));
        return new TestActor(clubId, person.PersonId, person.AccountId, membership, person.Name);
    }

    public static async Task<Guid> AssignRoleAsync(PostgresFixture db, TestActor member, RoleCode role, Guid? unitId = null)
    {
        var assignment = Guid.CreateVersion7();
        object unit = unitId is { } value ? value : DBNull.Value;
        await OwnerSql.ExecuteAsync(db.OwnerConnectionString, """
            INSERT INTO role_assignments (id, club_id, membership_id, role, unit_id, started_at)
                VALUES (@id, @club, @membership, @role, @unit, now())
            """,
            ("id", assignment), ("club", member.ClubId), ("membership", member.MembershipId), ("role", role.ToString()), ("unit", unit));
        return assignment;
    }

    public static async Task<TestActor> CreateMemberAsync(PostgresFixture db, Guid clubId, string name, int? age = 30, Sex sex = Sex.Female,
        RoleCode? role = null, Guid? unitId = null, TimeProvider? clock = null)
    {
        var person = await CreatePersonAsync(db, name, age, sex, clock: clock);
        var member = await AddMemberAsync(db, clubId, person);
        if (role is { } value)
            await AssignRoleAsync(db, member, value, unitId);
        return member;
    }

    public static async Task<TestClubA> CreateClubAAsync(PostgresFixture db, TimeProvider? clock = null)
    {
        var club = await CreateClubAsync(db, "Clube Águias", "Cidade Norte");
        var unitX = await CreateUnitAsync(db, club, "Unidade X", Sex.Female);
        var unitY = await CreateUnitAsync(db, club, "Unidade Y", Sex.Male);
        return new TestClubA(club, unitX, unitY,
            await CreateMemberAsync(db, club, "Aurora", 38, Sex.Female, RoleCode.Director, clock: clock),
            await CreateMemberAsync(db, club, "Caio", 30, Sex.Male, RoleCode.Secretary, clock: clock),
            await CreateMemberAsync(db, club, "Dalva", 27, Sex.Female, RoleCode.Treasurer, clock: clock),
            await CreateMemberAsync(db, club, "Iara", 24, Sex.Female, clock: clock),
            await CreateMemberAsync(db, club, "Lina", 26, Sex.Female, RoleCode.Counselor, unitX, clock));
    }

    public static async Task<TestClubB> CreateClubBAsync(PostgresFixture db, TimeProvider? clock = null)
    {
        var club = await CreateClubAsync(db, "Clube Corujas", "Cidade Sul");
        var unitZ = await CreateUnitAsync(db, club, "Unidade Z", Sex.Male);
        return new TestClubB(club, unitZ, await CreateMemberAsync(db, club, "Bento", 40, Sex.Male, RoleCode.Director, clock: clock));
    }

    // R0: clube com um membro. O membro tem o cargo de capelão para continuar lendo os membros (PERM-PRV-002).
    public static async Task<TestMember> CreateClubWithMemberAsync(PostgresFixture db, string personName)
    {
        var club = await CreateClubAsync(db);
        var member = await CreateMemberAsync(db, club, personName, role: RoleCode.Chaplain);
        return new TestMember(club, member.PersonId, member.AccountId, member.MembershipId);
    }

    public static Task EndMembershipAsync(PostgresFixture db, Guid membershipId) =>
        OwnerSql.ExecuteAsync(db.OwnerConnectionString,
            "UPDATE memberships SET status = 'Ended', ended_at = now() WHERE id = @id", ("id", membershipId));
}
