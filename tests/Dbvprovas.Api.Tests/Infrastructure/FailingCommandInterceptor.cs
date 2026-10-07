using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Dbvprovas.Api.Tests.Infrastructure;

// CA-AUD-004, CA-AUD-006
public sealed class FailingCommandInterceptor(string tableName) : DbCommandInterceptor
{
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
        command.CommandText.Contains(tableName, StringComparison.Ordinal)
            ? throw new NpgsqlException("Simulated database failure")
            : ValueTask.FromResult(result);
}
