using Dbvprovas.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Tenancy;

// Travas de linha das disputas do R1 (D-140, RNF-AUD-007). Ordem global, sempre a mesma, para não haver deadlock:
// consentimento → participação (por id) → atribuição. Exigem transação aberta; sob RLS, só travam o que o clube
// da requisição pode alterar.
public static class RowLocks
{
    public static async Task LockMembershipsAsync(AppDbContext db, IReadOnlyCollection<Guid> membershipIds, CancellationToken ct)
    {
        RequireTransaction(db);
        var ids = membershipIds.Distinct().ToArray();
        await db.Database.ExecuteSqlAsync($"SELECT id FROM memberships WHERE id = ANY({ids}) ORDER BY id FOR UPDATE", ct);
    }

    // exclusive: true para quem altera o consentimento (revogação); false para quem só depende dele (aprovação, reativação).
    public static async Task LockConsentAsync(AppDbContext db, Guid consentId, bool exclusive, CancellationToken ct)
    {
        RequireTransaction(db);
        if (exclusive)
            await db.Database.ExecuteSqlAsync($"SELECT id FROM consents WHERE id = {consentId} FOR UPDATE", ct);
        else
            await db.Database.ExecuteSqlAsync($"SELECT id FROM consents WHERE id = {consentId} FOR SHARE", ct);
    }

    private static void RequireTransaction(AppDbContext db)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Row locks require an open transaction (RNF-AUD-007).");
    }
}
