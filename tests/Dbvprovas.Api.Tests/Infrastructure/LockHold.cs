using Npgsql;

namespace Dbvprovas.Api.Tests.Infrastructure;

// Segura a trava de uma linha numa transação do dono, para o teste montar a disputa na ordem que quer (RNF-AUD-007, D-140):
// o pedido do app fica esperando a trava, o teste muda a linha e confirma, e o pedido segue com o estado novo.
public sealed class LockHold : IAsyncDisposable
{
    private readonly PostgresFixture _db;
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;

    private LockHold(PostgresFixture db, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        _db = db;
        _connection = connection;
        _transaction = transaction;
    }

    public static async Task<LockHold> ForUpdateAsync(PostgresFixture db, string table, Guid id)
    {
        var connection = new NpgsqlConnection(db.OwnerConnectionString);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand($"SELECT 1 FROM {table} WHERE id = @id FOR UPDATE", connection, transaction);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
        return new LockHold(db, connection, transaction);
    }

    // Espera até 10 s alguma sessão do app ficar parada esperando a trava DESTA conexão. O dono não vê o wait_event das
    // sessões de outro papel, mas vê o pid e quem as bloqueia; e o filtro pelo pid não confunde com outro teste em paralelo.
    public async Task WaitForBlockedAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (await OwnerSql.ScalarAsync<bool>(_db.OwnerConnectionString,
                    "SELECT EXISTS (SELECT 1 FROM pg_stat_activity a WHERE a.usename = 'dbv_app' AND @holder = ANY (pg_blocking_pids(a.pid)))",
                    ("holder", _connection.ProcessID)))
                return;
            await Task.Delay(50);
        }

        throw new TimeoutException("No app session is waiting for this lock.");
    }

    public async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, _connection, _transaction);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    public Task CommitAsync() => _transaction.CommitAsync();

    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
