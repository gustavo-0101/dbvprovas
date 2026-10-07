using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Tenancy;
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

    public static readonly IReadOnlyList<string> ClubAMemberNames = ["Aurora", "Caio", "Dalva"];
    public static readonly IReadOnlyList<string> ClubBMemberNames = ["Bento", "Elisa", "Fábio"];

    public static readonly IReadOnlyList<DevAccount> Accounts =
    [
        new(AuroraAccount, "Aurora — Clube Águias"),
        new(BentoAccount, "Bento — Clube Corujas"),
    ];

    private static readonly (Guid Person, Guid Membership, Guid Club, string Name)[] Members =
    [
        (AuroraPerson, Id("000000030001"), ClubA, "Aurora"),
        (Id("000000010002"), CaioMembership, ClubA, "Caio"),
        (Id("000000010003"), Id("000000030003"), ClubA, "Dalva"),
        (BentoPerson, Id("000000040001"), ClubB, "Bento"),
        (Id("000000020002"), ElisaMembership, ClubB, "Elisa"),
        (Id("000000020003"), Id("000000040003"), ClubB, "Fábio"),
    ];

    public static async Task ApplyAsync(AppDbContext db, TimeProvider clock, CancellationToken ct = default)
    {
        if (await db.Clubs.IgnoreQueryFilters().AnyAsync(c => c.Id == ClubA, ct))
            return;

        var now = clock.GetUtcNow();
        db.Clubs.AddRange(new Club { Id = ClubA, Name = "Clube Águias" }, new Club { Id = ClubB, Name = "Clube Corujas" });
        foreach (var (person, membership, club, name) in Members)
        {
            db.Persons.Add(new Person { Id = person, Name = name });
            db.Memberships.Add(new Membership { Id = membership, ClubId = club, PersonId = person, StartedAt = now });
        }

        db.Accounts.AddRange(
            new Account { Id = AuroraAccount, PersonId = AuroraPerson },
            new Account { Id = BentoAccount, PersonId = BentoPerson });
        await db.SaveChangesAsync(ct);
    }

    private static Guid Id(string tail) => Guid.Parse($"00000000-0000-7000-8000-{tail}");
}
