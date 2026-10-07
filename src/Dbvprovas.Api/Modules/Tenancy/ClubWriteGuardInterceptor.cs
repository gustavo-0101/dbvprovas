using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dbvprovas.Api.Modules.Tenancy;

// RN-TEN-001
public sealed class ClubWriteGuardInterceptor(ClubContext context) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Check(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Check(DbContext? db)
    {
        if (db is null)
            return;

        var outside = db.ChangeTracker.Entries<IClubOwned>().Any(e =>
            e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
            && e.Entity.ClubId != context.ClubId);
        if (outside)
            throw new ClubIsolationException();
    }
}

public sealed class ClubIsolationException()
    : InvalidOperationException("Write outside the current club context (RN-TEN-001).");
