using Dbvprovas.Api.Infrastructure;
using Dbvprovas.TestSupport;

namespace Dbvprovas.Api.Tests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgresDatabase _database = new();

    public string OwnerConnectionString => _database.OwnerConnectionString;
    public string AppConnectionString => _database.AppConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        await DevDatabase.MigrateAsync(OwnerConnectionString);
        await DevDatabase.SeedAsync(OwnerConnectionString);
    }

    // Banco só com o esquema, sem o seed (CA-ID-001).
    public async Task<DatabaseCredentials> CreateMigratedDatabaseAsync(string name)
    {
        var credentials = await _database.CreateDatabaseAsync(name);
        await DevDatabase.MigrateAsync(credentials.Owner);
        return credentials;
    }

    public ValueTask DisposeAsync() => _database.DisposeAsync();
}
