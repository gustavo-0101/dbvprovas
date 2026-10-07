using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Dbvprovas.TestSupport;

public sealed record DatabaseCredentials(string Owner, string App);

// Postgres 18 descartável com os mesmos papéis do dev (docker/postgres/roles.sql, D-121).
public sealed class PostgresDatabase : IAsyncDisposable
{
    // O logger padrão imprime argumentos do psql com credenciais (RNF-AUD-004).
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18")
        .WithLogger(NullLogger.Instance)
        .Build();
    private readonly string _ownerPassword = NewSecret();
    private readonly string _appPassword = NewSecret();

    public string OwnerConnectionString => ConnectionString("dbvprovas", "dbv_owner", _ownerPassword);
    public string AppConnectionString => ConnectionString("dbvprovas", "dbv_app", _appPassword);

    public async Task StartAsync()
    {
        await _container.StartAsync();
        var roles = await File.ReadAllBytesAsync(Path.Combine(RepoPaths.Root, "docker", "postgres", "roles.sql"));
        await _container.CopyAsync(roles, "/tmp/roles.sql");
        await PsqlAsync("-v", $"owner_password={_ownerPassword}", "-v", $"app_password={_appPassword}", "-f", "/tmp/roles.sql");
    }

    public async Task<DatabaseCredentials> CreateDatabaseAsync(string name)
    {
        await PsqlAsync("-c", $"CREATE DATABASE {name} OWNER dbv_owner");
        return new DatabaseCredentials(
            ConnectionString(name, "dbv_owner", _ownerPassword),
            ConnectionString(name, "dbv_app", _appPassword));
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    private async Task PsqlAsync(params string[] args)
    {
        var result = await _container.ExecAsync(["psql", "-v", "ON_ERROR_STOP=1", "-U", "postgres", "-d", "postgres", .. args]);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"psql failed: {result.Stderr}");
    }

    private string ConnectionString(string database, string user, string password) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = _container.Hostname,
            Port = _container.GetMappedPublicPort(5432),
            Database = database,
            Username = user,
            Password = password,
        }.ConnectionString;

    private static string NewSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}
