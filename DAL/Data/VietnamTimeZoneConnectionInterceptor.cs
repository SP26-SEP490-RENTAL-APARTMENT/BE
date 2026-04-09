using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DAL.Data;

public sealed class VietnamTimeZoneConnectionInterceptor : DbConnectionInterceptor
{
    private const string SetTimeZoneSql = "SET time_zone = '+07:00';";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetConnectionTimeZone(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        await SetConnectionTimeZoneAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void SetConnectionTimeZone(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = SetTimeZoneSql;
        command.ExecuteNonQuery();
    }

    private static async Task SetConnectionTimeZoneAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SetTimeZoneSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
