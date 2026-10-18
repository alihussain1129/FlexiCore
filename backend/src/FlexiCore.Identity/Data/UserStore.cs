using Npgsql;
using NpgsqlTypes;

namespace FlexiCore.Identity.Data;

public sealed record UserRecord(Guid Id, string? Email, string PasswordHash, string Status, bool IsLocked, string? Username = null)
{
    public bool IsActive => Status == "active";
}

public sealed class UserStore(NpgsqlDataSource dataSource, AuditWriter audit)
{
    private const string SelectColumns =
        "select id, email, password_hash, status, (locked_until is not null and locked_until > now()), username from identity.users ";

    public async Task<UserRecord?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(SelectColumns + "where email_normalized = @e");
        command.Parameters.AddWithValue("e", normalizedEmail);
        return await ReadOneAsync(command, ct);
    }

    public async Task<UserRecord?> FindByNormalizedUsernameAsync(string normalizedUsername, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(SelectColumns + "where username_normalized = @u");
        command.Parameters.AddWithValue("u", normalizedUsername);
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
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
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

    /// <summary>
    /// Atomically creates the first local owner account. A transaction-scoped advisory lock serializes setup
    /// attempts across API instances; once any user exists, setup cannot create another account.
    /// </summary>
    public async Task<Guid?> TryCreateFirstAsync(
        string email, string normalizedEmail, string passwordHash, string? correlationId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);

        await using (var setupLock = new NpgsqlCommand("select pg_advisory_xact_lock(731946201)", connection, transaction))
            await setupLock.ExecuteNonQueryAsync(ct);

        await using (var anyUsers = new NpgsqlCommand("select exists(select 1 from identity.users)", connection, transaction))
        {
            if ((bool)(await anyUsers.ExecuteScalarAsync(ct))!)
                return null;
        }

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
                new AuditEvent("auth.local_setup", "success", id, "user", id.ToString(), correlationId), ct);
            await transaction.CommitAsync(ct);
            return id;
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == "ux_users_email_normalized")
        {
            return null;
        }
    }

    /// <summary>
    /// First local owner identified by username (no email). Same advisory lock and one-time guarantee as
    /// <see cref="TryCreateFirstAsync"/>; once any user exists it returns null.
    /// </summary>
    public async Task<Guid?> TryCreateFirstWithUsernameAsync(
        string username, string normalizedUsername, string passwordHash, string? correlationId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);

        await using (var setupLock = new NpgsqlCommand("select pg_advisory_xact_lock(731946201)", connection, transaction))
            await setupLock.ExecuteNonQueryAsync(ct);

        await using (var anyUsers = new NpgsqlCommand("select exists(select 1 from identity.users)", connection, transaction))
        {
            if ((bool)(await anyUsers.ExecuteScalarAsync(ct))!)
                return null;
        }

        try
        {
            Guid id;
            await using (var insert = new NpgsqlCommand(
                "insert into identity.users (username, username_normalized, password_hash) values (@u, @n, @h) returning id",
                connection, transaction))
            {
                insert.Parameters.AddWithValue("u", username);
                insert.Parameters.AddWithValue("n", normalizedUsername);
                insert.Parameters.AddWithValue("h", passwordHash);
                id = (Guid)(await insert.ExecuteScalarAsync(ct))!;
            }

            await audit.WriteAsync(connection, transaction,
                new AuditEvent("auth.local_setup", "success", id, "user", id.ToString(), correlationId), ct);
            await transaction.CommitAsync(ct);
            return id;
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == "ux_users_username_normalized")
        {
            return null;
        }
    }

    public async Task<bool> HasAnyUsersAsync(CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("select exists(select 1 from identity.users)");
        return (bool)(await command.ExecuteScalarAsync(ct))!;
    }

    /// <summary>
    /// Counts a failed password attempt atomically, in one statement, so concurrent attempts cannot undercount.
    /// The counter lives on the user row, which is found through the normalized email, so casing or whitespace
    /// variants of an address all hit the same counter. An expired lock restarts the count at 1.
    /// Returns true when this failure locked the account (and writes the lockout audit row in the same transaction).
    /// </summary>
    public async Task<bool> RegisterFailedLoginAsync(Guid userId, int maxAttempts, int lockMinutes, string? correlationId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);

        int count;
        await using (var update = new NpgsqlCommand(
            "update identity.users set " +
            "failed_login_count = case when locked_until is not null and locked_until <= now() then 1 else failed_login_count + 1 end, " +
            "locked_until = case when (case when locked_until is not null and locked_until <= now() then 1 else failed_login_count + 1 end) >= @max " +
            "then now() + @minutes * interval '1 minute' else null end " +
            "where id = @id returning failed_login_count", connection, transaction))
        {
            update.Parameters.Add(new NpgsqlParameter("max", NpgsqlDbType.Integer) { Value = maxAttempts });
            update.Parameters.Add(new NpgsqlParameter("minutes", NpgsqlDbType.Integer) { Value = lockMinutes });
            update.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = userId });
            count = (int)(await update.ExecuteScalarAsync(ct))!;
        }

        var locked = count >= maxAttempts;
        if (locked)
        {
            await audit.WriteAsync(connection, transaction,
                new AuditEvent("auth.lockout", "denied", userId, "user", userId.ToString(), correlationId,
                    new Dictionary<string, string> { ["reason"] = "too_many_failed_logins" }), ct);
        }

        await transaction.CommitAsync(ct);
        return locked;
    }

    private static async Task<UserRecord?> ReadOneAsync(NpgsqlCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new UserRecord(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.IsDBNull(5) ? null : reader.GetString(5))
            : null;
    }
}