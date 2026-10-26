using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Joining;

// Um pedido pendente por pessoa, de entrada ou de fundação (RN-JOIN-006, D-138). A trava consultiva serializa dois
// envios da mesma pessoa; pedido de entrada vencido já não conta (D-165). Exige transação e o contexto da própria pessoa.
public static class PendingRequestGuard
{
    public static async Task<bool> TryAcquireAsync(AppDbContext db, Guid personId, DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("PendingRequestGuard requires an open transaction (RN-JOIN-006).");

        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"pending-request:" + personId}, 0))", ct);
        var queuedJoin = await db.JoinRequests.AnyAsync(j => j.CandidatePersonId == personId
            && j.Status == JoinRequestStatus.Queued && j.ExpiresAt > now, ct);
        var pendingFounding = await db.FoundingRequests.AnyAsync(f => f.RequesterPersonId == personId
            && f.Status == FoundingRequestStatus.Pending, ct);
        return !queuedJoin && !pendingFounding;
    }
}
