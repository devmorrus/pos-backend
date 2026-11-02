using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MorrusPOS.Application.Common.Interfaces;
using Npgsql;

namespace MorrusPOS.Infrastructure.Persistence;

/// <summary>
/// Sets the PostgreSQL tenant GUC for every physical connection checkout.
/// RLS policies use this value as their database-level tenant boundary.
/// </summary>
public sealed class TenantDbConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ICurrentUserService _currentUser;

    public TenantDbConnectionInterceptor(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetTenantAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetTenantAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
        base.ConnectionOpened(connection, eventData);
    }

    private async Task SetTenantAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is not NpgsqlConnection npgsql)
            return;

        await using var command = npgsql.CreateCommand();
        command.CommandText = "select set_config('app.business_id', @business_id, false);";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "business_id";
        parameter.Value = _currentUser.BusinessId?.ToString() ?? string.Empty;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
