using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dbvprovas.Api.Modules.Tenancy;

// RN-TEN-001, D-121, D-124
public sealed class ClubSessionInterceptor(ClubContext context) : DbConnectionInterceptor
{
    private const string Sql = "SELECT set_config('app.club_id', @club, false), set_config('app.person_id', @person, false), set_config('app.platform_admin', @admin, false)";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = Build(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = Build(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand Build(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = Sql;
        Add(command, "club", context.ClubId?.ToString());
        Add(command, "person", context.PersonId?.ToString());
        Add(command, "admin", context.IsPlatformAdmin ? "true" : null);
        return command;
    }

    private static void Add(DbCommand command, string name, string? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? string.Empty;
        command.Parameters.Add(parameter);
    }
}
