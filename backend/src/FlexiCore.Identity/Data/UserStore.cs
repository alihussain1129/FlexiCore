using Npgsql;
using NpgsqlTypes;

namespace FlexiCore.Identity.Data;

public sealed record UserRecord(Guid Id, string Email, string PasswordHash, string Status)
{
    public bool IsActive => Status == "active";
}

public sealed class UserStore(NpgsqlDataSource dataSource, AuditWriter audit)
{
    private const string SelectColumns = "select id, email, password_hash, status from identity.users ";

    public async Task<UserRecord?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(SelectColumns + "where email_normalized = @e");
        command.Parameters.AddWithValue("e", normalizedEmail);
        return await ReadOneAsync(command, ct);
    }

    public async Task<UserRecord?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(SelectColumns + "where id = @id");
        command.Parameters.AddWithValue("id", id);
        return await ReadOneAsync(command, ct);
    }

    /// <summary>
    /// Inserts the user and its audit row in one transaction. Returns null if the normalized email already exists
    /// (enforced by the unique index, so concurrent registrations cannot both win).
    /// </summary>
    public async Task<Guid?> TryCreateAsync(string email, string normalizedEmail, string passwordHash, string? correlationId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            Guid id;
            await using (var insert = new NpgsqlCommand(
                "insert into identity.users (email, email_normalized, password_hash) values (@e, @n, @h) returning id",
                connection, transaction))
            {
                insert.Parameters.AddWithValue("e", email);
                insert.Parameters.AddWithValue("n", normalizedEmail);
                insert.Parameters.AddWithValue("h", passwordHash);
                id = (Guid)(await insert.ExecuteScalarAsync(ct))!;
            }

            await audit.WriteAsync(connection, transaction,
                new AuditEvent("auth.register", "success", id, "user", id.ToString(), correlationId), ct);
            await transaction.CommitAsync(ct);
            return id;
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == "ux_users_email_normalized")
        {
            return null; // the transaction is disposed without commit, so nothing is kept
        }
    }

    /// <summary>Records the login time and, when the stored hash is outdated, replaces it with a fresh one.</summary>
    public async Task UpdateAfterLoginAsync(Guid userId, string? newPasswordHash, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(
            "update identity.users set last_login_at = now(), password_hash = coalesce(@hash, password_hash) where id = @id");
        command.Parameters.Add(new NpgsqlParameter("hash", NpgsqlDbType.Text) { Value = (object?)newPasswordHash ?? DBNull.Value });
        command.Parameters.AddWithValue("id", userId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<UserRecord?> ReadOneAsync(NpgsqlCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new UserRecord(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3))
            : null;
    }
}