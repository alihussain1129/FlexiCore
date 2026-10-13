using Npgsql;

namespace FlexiCore.Tenancy;

public interface ITenantConnectionFactory
{
    Task<TenantScope> OpenAsync(TenantContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Opens a transaction and sets app.user_id / app.company_id with
/// set_config(..., is_local := true), so the values vanish at commit or
/// rollback and can never leak to the next user of a pooled connection.
/// </summary>
public sealed class TenantConnectionFactory : ITenantConnectionFactory
{
    private const string SetContextSql =
        "SELECT set_config('app.user_id', @user_id, true), " +
        "set_config('app.company_id', @company_id, true)";

    private readonly NpgsqlDataSource _dataSource;

    public TenantConnectionFactory(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<TenantScope> OpenAsync(
        TenantContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await using var command = new NpgsqlCommand(SetContextSql, connection, transaction);
                command.Parameters.AddWithValue("user_id", context.UserId.ToString());
                command.Parameters.AddWithValue("company_id", context.CompanyId?.ToString() ?? string.Empty);
                await command.ExecuteNonQueryAsync(cancellationToken);

                return new TenantScope(connection, transaction);
            }
            catch
            {
                await transaction.DisposeAsync();
                throw;
            }
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}