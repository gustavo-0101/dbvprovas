using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Dbvprovas.Api.Tests.Organization;

// CA-AUD-010: falha simulada ao gravar numa tabela, para provar que a decisão não fica pela metade.
public sealed class FailingInsertInterceptor(string tableName) : DbCommandInterceptor
{
    // O EF com Npgsql escreve o nome entre aspas (INSERT INTO "audit_entries"); SQL escrito à mão, sem.
    private bool Matches(DbCommand command) =>
        command.CommandText.Contains($"INSERT INTO \"{tableName}\"", StringComparison.Ordinal)
        || command.CommandText.Contains($"INSERT INTO {tableName}", StringComparison.Ordinal);

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
        Matches(command) ? throw new NpgsqlException("Simulated database failure") : ValueTask.FromResult(result);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
        Matches(command) ? throw new NpgsqlException("Simulated database failure") : ValueTask.FromResult(result);
}
