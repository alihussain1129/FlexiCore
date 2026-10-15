using System.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;

namespace FlexiCore.Identity.Data;

public enum RefreshOutcome
{
    Rotated,
    Invalid,
    Revoked,
    Reused,
    Expired,
    UserInactive
}

public sealed record RefreshResult(RefreshOutcome Outcome, Guid? UserId = null, Guid? FamilyId = null);

/// <summary>
/// Refresh sessions. A "family" is the chain of tokens that starts at one login; its id is the "sid" claim.
/// Every operation that changes a family first takes a Postgres advisory lock on that family, so rotation, reuse
/// handling and logout are serialized per family: concurrent requests cannot both rotate one token, and a logout
/// can never miss a token that a concurrent refresh has just issued.
/// Only token hashes are stored or audited; plaintext tokens never reach this class.
/// </summary>
public sealed class RefreshSessionStore(NpgsqlDataSource dataSource, AuditWriter audit)
{
    private sealed record SessionRow(
        Guid Id, Guid UserId, Guid FamilyId, byte[] TokenHash, bool Rotated, bool Revoked, bool Expired, string UserStatus);

    /// <summary>
    /// Completes a successful login in one transaction: resets the failure counter, stores the last-login time and any
    /// upgraded password hash, opens a new token family (absolute lifetime 90 days) and audits the login.
    /// </summary>
    public async Task<Guid> CompleteLoginAsync(
        Guid userId, string? newPasswordHash, byte[] tokenHash, int refreshDays, string? correlationId, CancellationToken ct)
    {
        var familyId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);

        await using (var update = new NpgsqlCommand(
            "update identity.users set last_login_at = now(), password_hash = coalesce(@hash, password_hash), " +
            "failed_login_count = 0, locked_until = null where id = @id", connection, transaction))
        {
            update.Parameters.Add(new NpgsqlParameter("hash", NpgsqlDbType.Text) { Value = (object?)newPasswordHash ?? DBNull.Value });
            update.Parameters.Add(Uuid("id", userId));
            await update.ExecuteNonQueryAsync(ct);
        }

        await using (var insert = new NpgsqlCommand(
            "insert into identity.refresh_sessions (user_id, family_id, token_hash, expires_at, family_expires_at) " +
            "values (@user, @family, @hash, " +
            "least(now() + @days * interval '1 day', now() + @family_days * interval '1 day'), " +
            "now() + @family_days * interval '1 day')", connection, transaction))
        {
            insert.Parameters.Add(Uuid("user", userId));
            insert.Parameters.Add(Uuid("family", familyId));
            insert.Parameters.Add(Bytes("hash", tokenHash));
            insert.Parameters.Add(Int("days", refreshDays));
            insert.Parameters.Add(Int("family_days", Settings.IdentitySettings.FamilyLifetimeDays));
            await insert.ExecuteNonQueryAsync(ct);
        }

        await audit.WriteAsync(connection, transaction,
            new AuditEvent("auth.login", "success", userId, "user", userId.ToString(), correlationId,
                new Dictionary<string, string> { ["session"] = familyId.ToString() }), ct);

        await transaction.CommitAsync(ct);
        return familyId;
    }

    /// <summary>
    /// Rotates a refresh token atomically. Every failure is committed together with its audit row, and the caller
    /// returns the same generic 401 for all of them. Presenting an already-rotated token revokes the whole family.
    /// </summary>
    public async Task<RefreshResult> RotateAsync(
        byte[] presentedHash, byte[] newHash, int refreshDays, string? correlationId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);

        var found = await FindAsync(connection, transaction, presentedHash, ct);
        if (found is null)
        {
            await AuditAsync(connection, transaction, "auth.refresh", "failure", null, null, correlationId, "unknown_token", ct);
            await transaction.CommitAsync(ct);
            return new RefreshResult(RefreshOutcome.Invalid);
        }

        await LockFamilyAsync(connection, transaction, found.Value.FamilyId, ct);

        // Re-read after the lock so the decision is made on the latest committed state.
        var row = await ReadRowAsync(connection, transaction, presentedHash, ct);
        if (row is null || !CryptographicOperations.FixedTimeEquals(row.TokenHash, presentedHash))
        {
            await AuditAsync(connection, transaction, "auth.refresh", "failure", null, null, correlationId, "unknown_token", ct);
            await transaction.CommitAsync(ct);
            return new RefreshResult(RefreshOutcome.Invalid);
        }

        if (row.Revoked)
        {
            await AuditAsync(connection, transaction, "auth.refresh", "failure", row.UserId, row.FamilyId, correlationId, "revoked", ct);
            await transaction.CommitAsync(ct);
            return new RefreshResult(RefreshOutcome.Revoked);
        }

        if (row.Rotated)
        {
            // An already-rotated token is being presented again: treat the family as stolen and revoke all of it.
            await RevokeFamilyAsync(connection, transaction, row.FamilyId, "reuse_detected", ct);
            await AuditAsync(connection, transaction, "auth.refresh", "failure", row.UserId, row.FamilyId, correlationId, "reuse_detected", ct);
            await AuditAsync(connection, transaction, "auth.session.revoked", "success", row.UserId, row.FamilyId, correlationId, "reuse_detected", ct);
            await transaction.CommitAsync(ct);
            return new RefreshResult(RefreshOutcome.Reused);
        }

        if (row.Expired)
        {
            await AuditAsync(connection, transaction, "auth.refresh", "failure", row.UserId, row.FamilyId, correlationId, "expired", ct);
            await transaction.CommitAsync(ct);
            return new RefreshResult(RefreshOutcome.Expired);
        }

        if (row.UserStatus != "active")
        {
            await RevokeFamilyAsync(connection, transaction, row.FamilyId, "user_inactive", ct);
            await AuditAsync(connection, transaction, "auth.refresh", "failure", row.UserId, row.FamilyId, correlationId, "user_inactive", ct);
            await AuditAsync(connection, transaction, "auth.session.revoked", "success", row.UserId, row.FamilyId, correlationId, "user_inactive", ct);
            await transaction.CommitAsync(ct);
            return new RefreshResult(RefreshOutcome.UserInactive);
        }

        await using (var rotate = new NpgsqlCommand(
            "update identity.refresh_sessions set rotated_at = now() where id = @id", connection, transaction))
        {
            rotate.Parameters.Add(Uuid("id", row.Id));
            await rotate.ExecuteNonQueryAsync(ct);
        }

        // The new token never outlives the family: expires_at = least(now + token lifetime, family_expires_at).
        await using (var insert = new NpgsqlCommand(
            "insert into identity.refresh_sessions (user_id, family_id, token_hash, expires_at, family_expires_at) " +
            "select user_id, family_id, @new, least(now() + @days * interval '1 day', family_expires_at), family_expires_at " +
            "from identity.refresh_sessions where id = @id", connection, transaction))
        {
            insert.Parameters.Add(Bytes("new", newHash));
            insert.Parameters.Add(Int("days", refreshDays));
            insert.Parameters.Add(Uuid("id", row.Id));
            await insert.ExecuteNonQueryAsync(ct);
        }

        await AuditAsync(connection, transaction, "auth.refresh", "success", row.UserId, row.FamilyId, correlationId, null, ct);
        await transaction.CommitAsync(ct);
        return new RefreshResult(RefreshOutcome.Rotated, row.UserId, row.FamilyId);
    }

    /// <summary>Revokes the whole family of the presented token (any token of the family works). Idempotent.</summary>
    public async Task LogoutAsync(byte[] presentedHash, string? correlationId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);

        var found = await FindAsync(connection, transaction, presentedHash, ct);
        if (found is null)
        {
            await AuditAsync(connection, transaction, "auth.logout", "failure", null, null, correlationId, "unknown_token", ct);
            await transaction.CommitAsync(ct);
            return;
        }

        var (familyId, userId) = found.Value;
        await LockFamilyAsync(connection, transaction, familyId, ct);

        var revoked = await RevokeFamilyAsync(connection, transaction, familyId, "logout", ct);
        if (revoked > 0)
        {
            await AuditAsync(connection, transaction, "auth.logout", "success", userId, familyId, correlationId, null, ct);
            await AuditAsync(connection, transaction, "auth.session.revoked", "success", userId, familyId, correlationId, "logout", ct);
        }
        else
        {
            await AuditAsync(connection, transaction, "auth.logout", "failure", userId, familyId, correlationId, "already_revoked", ct);
        }

        await transaction.CommitAsync(ct);
    }

    /// <summary>True while the family has an unrevoked token and has not passed its absolute expiry. Used on every authenticated request.</summary>
    public async Task<bool> IsSessionActiveAsync(Guid userId, Guid familyId, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(
            "select exists (select 1 from identity.refresh_sessions " +
            "where family_id = @family and user_id = @user and revoked_at is null and family_expires_at > now())");
        command.Parameters.Add(Uuid("family", familyId));
        command.Parameters.Add(Uuid("user", userId));
        return (bool)(await command.ExecuteScalarAsync(ct))!;
    }

    private static async Task<(Guid FamilyId, Guid UserId)?> FindAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, byte[] hash, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "select family_id, user_id from identity.refresh_sessions where token_hash = @hash", connection, transaction);
        command.Parameters.Add(Bytes("hash", hash));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? (reader.GetGuid(0), reader.GetGuid(1)) : null;
    }

    private static async Task LockFamilyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid familyId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "select pg_advisory_xact_lock(hashtextextended(@family, 0))", connection, transaction);
        command.Parameters.Add(new NpgsqlParameter("family", NpgsqlDbType.Text) { Value = familyId.ToString() });
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<SessionRow?> ReadRowAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, byte[] hash, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "select s.id, s.user_id, s.family_id, s.token_hash, s.rotated_at is not null, s.revoked_at is not null, " +
            "(s.expires_at <= now() or s.family_expires_at <= now()), u.status " +
            "from identity.refresh_sessions s join identity.users u on u.id = s.user_id where s.token_hash = @hash",
            connection, transaction);
        command.Parameters.Add(Bytes("hash", hash));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new SessionRow(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetFieldValue<byte[]>(3),
                reader.GetBoolean(4), reader.GetBoolean(5), reader.GetBoolean(6), reader.GetString(7))
            : null;
    }

    private static async Task<int> RevokeFamilyAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid familyId, string reason, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "update identity.refresh_sessions set revoked_at = now(), revoked_reason = @reason " +
            "where family_id = @family and revoked_at is null", connection, transaction);
        command.Parameters.Add(new NpgsqlParameter("reason", NpgsqlDbType.Text) { Value = reason });
        command.Parameters.Add(Uuid("family", familyId));
        return await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Audit rows carry ids and a reason only: never a token, a token hash or a password.</summary>
    private Task AuditAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string action, string outcome,
        Guid? userId, Guid? familyId, string? correlationId, string? reason, CancellationToken ct) =>
        audit.WriteAsync(connection, transaction,
            new AuditEvent(action, outcome, userId, familyId is null ? null : "session", familyId?.ToString(), correlationId,
                reason is null ? null : new Dictionary<string, string> { ["reason"] = reason }), ct);

    private static NpgsqlParameter Bytes(string name, byte[] value) => new(name, NpgsqlDbType.Bytea) { Value = value };
    private static NpgsqlParameter Uuid(string name, Guid value) => new(name, NpgsqlDbType.Uuid) { Value = value };
    private static NpgsqlParameter Int(string name, int value) => new(name, NpgsqlDbType.Integer) { Value = value };
}