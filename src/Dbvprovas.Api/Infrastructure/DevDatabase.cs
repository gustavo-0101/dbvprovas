using Dbvprovas.Api.Modules.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Infrastructure;

// Migrations e seed com o papel dono das tabelas (D-121). Em dev roda ao subir a API;
// nos testes, pela fixture. Sem interceptadores: o dono não passa pelo RLS.
public static class DevDatabase
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var owner = services.GetRequiredService<IConfiguration>().GetConnectionString("Owner")
            ?? throw new InvalidOperationException("Connection string 'Owner' is missing.");
        await MigrateAsync(owner);
        await SeedAsync(owner);
    }

    public static async Task MigrateAsync(string ownerConnectionString, CancellationToken ct = default)
    {
        await using var db = Create(ownerConnectionString);
        await db.Database.MigrateAsync(ct);
    }

    public static async Task SeedAsync(string ownerConnectionString, CancellationToken ct = default)
    {
        await using var db = Create(ownerConnectionString);
        await DevSeed.ApplyAsync(db, TimeProvider.System, ct);
    }

    private static AppDbContext Create(string ownerConnectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ownerConnectionString).Options, new ClubContext());
}
