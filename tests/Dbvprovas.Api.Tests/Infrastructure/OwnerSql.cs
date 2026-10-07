using Npgsql;

namespace Dbvprovas.Api.Tests.Infrastructure;

// Acesso como dono das tabelas, sem RLS: só para preparar e conferir dados nos testes.
public static class OwnerSql
{
    public static async Task<T> ScalarAsync<T>(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = Build(connection, sql, parameters);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    public static async Task<int> ExecuteAsync(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = Build(connection, sql, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    private static NpgsqlCommand Build(NpgsqlConnection connection, string sql, (string Name, object Value)[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return command;
    }
}
