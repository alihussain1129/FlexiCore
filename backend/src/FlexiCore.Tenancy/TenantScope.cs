using Npgsql;

namespace FlexiCore.Tenancy;

/// <summary>
/// A connection plus transaction with the tenant context applied.
/// Dispose without committing to roll back.
/// </summary>
public sealed class TenantScope : IAsyncDisposable
{
    internal TenantScope(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        Connection = connection;
        Transaction = transaction;
    }

    public NpgsqlConnection Connection { get; }

    public NpgsqlTransaction Transaction { get; }

    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        Transaction.CommitAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await Transaction.DisposeAsync();
        await Connection.DisposeAsync();
    }
}