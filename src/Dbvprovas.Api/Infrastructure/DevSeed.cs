using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Organization;
using Dbvprovas.Api.Modules.Privacy;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Infrastructure;

public sealed record DevAccount(Guid AccountId, string Label);

// Dados fictícios do ambiente de dev e dos testes (RF-TEN-003, PC-04). Nunca roda fora de dev.
public static class DevSeed
{
    public static readonly Guid ClubA = Id("00000000000a");
    public static readonly Guid ClubB = Id("00000000000b");

    public static readonly Guid AuroraPerson = Id("000000010001");
    public static readonly Guid BentoPerson = Id("000000020001");
    public static readonly Guid CaioMembership = Id("000000030002");
    public static readonly Guid ElisaMembership = Id("000000040002");
    public static readonly Guid AuroraAccount = Id("000000050001");
    public static readonly Guid BentoAccount = Id("000000060001");
    public static readonly Guid AdminAlfaPerson = Id("000000070001");
    public static readonly Guid AdminAlfaAccount = Id("000000080001");

    public const string AuroraSubject = "seed-aurora";
    public const string BentoSubject = "seed-bento";
    public const string AdminAlfaSubject = "seed-admin-alfa";

    public static readonly IReadOnlyList<string> ClubAMemberNames = ["Aurora", "Caio", "Dalva"];
    public static readonly IReadOnlyList<string> ClubBMemberNames = ["Bento", "Elisa", "Fábio"];

    public static readonly IReadOnlyList<DevAccount> Accounts =
    [
        new(AuroraAccount, "Aurora — Clube Águias"),
        new(BentoAccount, "Bento — Clube Corujas"),
    ];

    private sealed record SeedMember(Guid Person, Guid Membership, Guid Club, string Name, DateOnly BirthDate, Sex Sex, RoleCode? Role);

    private static readonly SeedMember[] Members =
    [
        new(AuroraPerson, Id("000000030001"), ClubA, "Aurora", new(1988, 3, 14), Sex.Female, RoleCode.Director),
        new(Id("000000010002"), CaioMembership, ClubA, "Caio", new(1996, 7, 2), Sex.Male, RoleCode.Secretary),
        new(Id("000000010003"), Id("000000030003"), ClubA, "Dalva", new(1999, 5, 20), Sex.Female, RoleCode.Treasurer),
        new(BentoPerson, Id("000000040001"), ClubB, "Bento", new(1986, 1, 9), Sex.Male, RoleCode.Director),
        new(Id("000000020002"), ElisaMembership, ClubB, "Elisa", new(1993, 11, 30), Sex.Female, RoleCode.Secretary),
        new(Id("000000020003"), Id("000000040003"), ClubB, "Fábio", new(1990, 8, 17), Sex.Male, null),
    ];

    public static async Task ApplyAsync(AppDbContext db, TimeProvider clock, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        if (!await db.Clubs.IgnoreQueryFilters().AnyAsync(c => c.Id == ClubA, ct))
        {
            db.Clubs.AddRange(
                new Club { Id = ClubA, Name = "Clube Águias", City = "Cidade Norte", Church = "Igreja Central", InviteCode = "AGUA2345", CreatedAt = now },
                new Club { Id = ClubB, Name = "Clube Corujas", City = "Cidade Sul", InviteCode = "CRUJ2345", CreatedAt = now });
            foreach (var m in Members)
            {
                db.Persons.Add(new Person { Id = m.Person, Name = m.Name, BirthDate = m.BirthDate, Sex = m.Sex, CreatedAt = now });
                db.Memberships.Add(new Membership { Id = m.Membership, ClubId = m.Club, PersonId = m.Person, StartedAt = now });
            }

            db.Accounts.AddRange(
                new Account { Id = AuroraAccount, PersonId = AuroraPerson, GoogleSubject = AuroraSubject, Email = "aurora@example.com", CreatedAt = now },
                new Account { Id = BentoAccount, PersonId = BentoPerson, GoogleSubject = BentoSubject, Email = "bento@example.com", CreatedAt = now });
            await db.SaveChangesAsync(ct);
        }

        await CompleteForR1Async(db, now, ct);
    }

    // Completa, sem duplicar, o que o R1 acrescentou a um seed criado no R0 (banco de dev antigo).
    private static async Task CompleteForR1Async(AppDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var m in Members)
        {
            var person = await db.Persons.IgnoreQueryFilters().SingleAsync(p => p.Id == m.Person, ct);
            person.BirthDate ??= m.BirthDate;
            person.Sex ??= m.Sex;
            if (m.Role is { } role && !await db.RoleAssignments.IgnoreQueryFilters().AnyAsync(r => r.MembershipId == m.Membership && r.Role == role, ct))
                db.RoleAssignments.Add(new RoleAssignment { Id = Guid.CreateVersion7(), ClubId = m.Club, MembershipId = m.Membership, Role = role, StartedAt = now });
            if (!await db.Consents.IgnoreQueryFilters().AnyAsync(c => c.PersonId == m.Person && c.ClubId == m.Club, ct))
                db.Consents.Add(new Consent
                {
                    Id = Guid.CreateVersion7(), PersonId = m.Person, ClubId = m.Club,
                    TermVersion = ConsentTerms.AdultOwnParticipationVersion, GrantedAt = now,
                });
        }

        foreach (var (accountId, subject, email) in new[] { (AuroraAccount, AuroraSubject, "aurora@example.com"), (BentoAccount, BentoSubject, "bento@example.com") })
        {
            var account = await db.Accounts.SingleAsync(a => a.Id == accountId, ct);
            if (account.GoogleSubject.StartsWith("r0-dev-", StringComparison.Ordinal))
            {
                account.GoogleSubject = subject;
                account.Email = email;
            }
        }

        foreach (var (clubId, city, church, invite) in new[] { (ClubA, "Cidade Norte", (string?)"Igreja Central", "AGUA2345"), (ClubB, "Cidade Sul", null, "CRUJ2345") })
        {
            var club = await db.Clubs.IgnoreQueryFilters().SingleAsync(c => c.Id == clubId, ct);
            if (club.City.Length == 0)
            {
                club.City = city;
                club.Church = church;
                club.InviteCode = invite;
            }
        }

        if (!await db.Accounts.AnyAsync(a => a.Id == AdminAlfaAccount, ct))
        {
            db.Persons.Add(new Person { Id = AdminAlfaPerson, Name = "Admin Alfa", BirthDate = new(1990, 4, 22), Sex = Sex.Female, CreatedAt = now });
            db.Accounts.Add(new Account
            {
                Id = AdminAlfaAccount, PersonId = AdminAlfaPerson, GoogleSubject = AdminAlfaSubject,
                Email = "admin.alfa@example.com", IsPlatformAdmin = true, CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static Guid Id(string tail) => Guid.Parse($"00000000-0000-7000-8000-{tail}");
}
