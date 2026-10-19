using FlexiCore.Devices.Security;
using Npgsql;
using NpgsqlTypes;

namespace FlexiCore.Devices.Data;

public sealed record DeviceSummary(
    Guid Id, string Name, bool IsOwner, DateTimeOffset CreatedAt, DateTimeOffset? LastSeenAt, DateTimeOffset? RevokedAt);

public sealed record DeviceCredential(Guid Id, string Name, bool IsOwner, byte[] SecretHash);

public enum PairOutcome { Paired, InvalidCode }

public sealed record PairResult(PairOutcome Outcome, Guid DeviceId = default, string? Secret = null, bool IsOwner = false);

public enum CodeOutcome { Created, TooManyOutstanding }

public sealed record CodeResult(CodeOutcome Outcome, string? Code = null, DateTimeOffset ExpiresAt = default);

public enum RevokeOutcome { Revoked, AlreadyRevoked, NotFound, LastOwner }

public sealed class DeviceStore(NpgsqlDataSource dataSource, DeviceAuditWriter audit)
{
    public const int MaxOutstandingCodes = 20;
    private static readonly TimeSpan LastSeenInterval = TimeSpan.FromMinutes(1);

    public async Task<CodeResult> CreatePairingCodeAsync(
        bool isOwner, Guid? createdByDeviceId, string actorKind, DateTimeOffset now, TimeSpan lifetime,
        string? correlationId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        await using (var gate = new NpgsqlCommand(
            "select pg_advisory_xact_lock(hashtextextended('devices.pairing_code', 0))", connection, transaction))
        {
            await gate.ExecuteNonQueryAsync(ct);
        }

        long outstanding;
        await using (var count = new NpgsqlCommand(
            "select count(*) from devices.pairing_codes where consumed_at is null and expires_at > @now",
            connection, transaction))
        {
            count.Parameters.Add(Timestamp("now", now));
            outstanding = (long)(await count.ExecuteScalarAsync(ct))!;
        }

        if (outstanding >= MaxOutstandingCodes)
        {
            await transaction.RollbackAsync(ct);
            return new CodeResult(CodeOutcome.TooManyOutstanding);
        }

        var code = PairingCode.Generate();
        var expiresAt = now + lifetime;

        await using (var insert = new NpgsqlCommand(
            "insert into devices.pairing_codes (code_hash, is_owner, created_by_device_id, expires_at) " +
            "values (@hash, @owner, @creator, @expires)", connection, transaction))
        {
            insert.Parameters.Add(new NpgsqlParameter("hash", NpgsqlDbType.Bytea) { Value = PairingCode.Hash(code) });
            insert.Parameters.Add(new NpgsqlParameter("owner", NpgsqlDbType.Boolean) { Value = isOwner });
            insert.Parameters.Add(new NpgsqlParameter("creator", NpgsqlDbType.Uuid) { Value = (object?)createdByDeviceId ?? DBNull.Value });
            insert.Parameters.Add(Timestamp("expires", expiresAt));
            await insert.ExecuteNonQueryAsync(ct);
        }

        await audit.WriteAsync(connection, transaction, new DeviceAuditEvent(
            "device.pairing_code.create", "success", null, actorKind, createdByDeviceId, correlationId,
            new Dictionary<string, string> { ["owner_code"] = isOwner ? "true" : "false" }), ct);

        await transaction.CommitAsync(ct);
        return new CodeResult(CodeOutcome.Created, code, expiresAt);
    }

    public async Task<PairResult> PairAsync(
        string normalizedCode, string deviceName, DateTimeOffset now, string? correlationId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        // Atomic single use: a concurrent second attempt blocks on this row, then finds it already consumed.
        var codeId = Guid.Empty;
        var isOwner = false;
        var found = false;
        await using (var consume = new NpgsqlCommand(
            "update devices.pairing_codes set consumed_at = @now " +
            "where code_hash = @hash and consumed_at is null and expires_at > @now returning id, is_owner",
            connection, transaction))
        {
            consume.Parameters.Add(Timestamp("now", now));
            consume.Parameters.Add(new NpgsqlParameter("hash", NpgsqlDbType.Bytea) { Value = PairingCode.Hash(normalizedCode) });
            await using var reader = await consume.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                codeId = reader.GetGuid(0);
                isOwner = reader.GetBoolean(1);
                found = true;
            }
        }

        if (!found)
        {
            await transaction.RollbackAsync(ct);
            await audit.WriteAsync(new DeviceAuditEvent(
                "device.pair", "denied", null, "anonymous", null, correlationId,
                new Dictionary<string, string> { ["reason"] = "invalid_code" }), ct);
            return new PairResult(PairOutcome.InvalidCode);
        }

        var secret = DeviceSecret.Generate();
        Guid deviceId;
        await using (var insert = new NpgsqlCommand(
            "insert into devices.devices (name, secret_hash, is_owner) values (@name, @hash, @owner) returning id",
            connection, transaction))
        {
            insert.Parameters.Add(new NpgsqlParameter("name", NpgsqlDbType.Text) { Value = deviceName });
            insert.Parameters.Add(new NpgsqlParameter("hash", NpgsqlDbType.Bytea) { Value = DeviceSecret.Hash(secret) });
            insert.Parameters.Add(new NpgsqlParameter("owner", NpgsqlDbType.Boolean) { Value = isOwner });
            deviceId = (Guid)(await insert.ExecuteScalarAsync(ct))!;
        }

        await using (var link = new NpgsqlCommand(
            "update devices.pairing_codes set consumed_by_device_id = @device where id = @id", connection, transaction))
        {
            link.Parameters.Add(new NpgsqlParameter("device", NpgsqlDbType.Uuid) { Value = deviceId });
            link.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = codeId });
            await link.ExecuteNonQueryAsync(ct);
        }

        await audit.WriteAsync(connection, transaction, new DeviceAuditEvent(
            "device.pair", "success", deviceId.ToString(), "anonymous", null, correlationId,
            new Dictionary<string, string>
            {
                ["device_name"] = deviceName,
                ["owner"] = isOwner ? "true" : "false",
            }), ct);

        await transaction.CommitAsync(ct);
        return new PairResult(PairOutcome.Paired, deviceId, secret, isOwner);
    }

    public async Task<DeviceCredential?> FindActiveCredentialAsync(Guid deviceId, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(
            "select name, is_owner, secret_hash from devices.devices where id = @id and revoked_at is null");
        command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = deviceId });
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new DeviceCredential(deviceId, reader.GetString(0), reader.GetBoolean(1), reader.GetFieldValue<byte[]>(2));
    }

    /// <summary>Records activity at most once a minute per device, so a busy client does not write on every request.</summary>
    public async Task TouchAsync(Guid deviceId, DateTimeOffset now, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(
            "update devices.devices set last_seen_at = @now " +
            "where id = @id and revoked_at is null and (last_seen_at is null or last_seen_at < @older)");
        command.Parameters.Add(Timestamp("now", now));
        command.Parameters.Add(Timestamp("older", now - LastSeenInterval));
        command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = deviceId });
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<DeviceSummary>> ListAsync(CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(
            "select id, name, is_owner, created_at, last_seen_at, revoked_at from devices.devices order by created_at, id");
        await using var reader = await command.ExecuteReaderAsync(ct);
        var devices = new List<DeviceSummary>();
        while (await reader.ReadAsync(ct))
        {
            devices.Add(new DeviceSummary(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetBoolean(2),
                Utc(reader.GetDateTime(3)),
                reader.IsDBNull(4) ? null : Utc(reader.GetDateTime(4)),
                reader.IsDBNull(5) ? null : Utc(reader.GetDateTime(5))));
        }

        return devices;
    }

    public async Task<RevokeOutcome> RevokeAsync(
        Guid deviceId, Guid actorDeviceId, DateTimeOffset now, string? correlationId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        // Serializes revocations so two concurrent calls cannot both remove the last owner device.
        await using (var gate = new NpgsqlCommand(
            "select pg_advisory_xact_lock(hashtextextended('devices.revoke', 0))", connection, transaction))
        {
            await gate.ExecuteNonQueryAsync(ct);
        }

        var exists = false;
        var alreadyRevoked = false;
        var isOwner = false;
        await using (var lookup = new NpgsqlCommand(
            "select is_owner, revoked_at is not null from devices.devices where id = @id", connection, transaction))
        {
            lookup.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = deviceId });
            await using var reader = await lookup.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                exists = true;
                isOwner = reader.GetBoolean(0);
                alreadyRevoked = reader.GetBoolean(1);
            }
        }

        if (!exists)
        {
            await transaction.RollbackAsync(ct);
            return RevokeOutcome.NotFound;
        }

        if (alreadyRevoked)
        {
            await transaction.RollbackAsync(ct);
            return RevokeOutcome.AlreadyRevoked;
        }

        if (isOwner)
        {
            long otherOwners;
            await using (var others = new NpgsqlCommand(
                "select count(*) from devices.devices where is_owner and revoked_at is null and id <> @id",
                connection, transaction))
            {
                others.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = deviceId });
                otherOwners = (long)(await others.ExecuteScalarAsync(ct))!;
            }

            if (otherOwners == 0)
            {
                await transaction.RollbackAsync(ct);
                await audit.WriteAsync(new DeviceAuditEvent(
                    "device.revoke", "denied", deviceId.ToString(), "device", actorDeviceId, correlationId,
                    new Dictionary<string, string> { ["reason"] = "last_owner" }), ct);
                return RevokeOutcome.LastOwner;
            }
        }

        await using (var revoke = new NpgsqlCommand(
            "update devices.devices set revoked_at = @now where id = @id and revoked_at is null", connection, transaction))
        {
            revoke.Parameters.Add(Timestamp("now", now));
            revoke.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = deviceId });
            await revoke.ExecuteNonQueryAsync(ct);
        }

        await audit.WriteAsync(connection, transaction, new DeviceAuditEvent(
            "device.revoke", "success", deviceId.ToString(), "device", actorDeviceId, correlationId,
            new Dictionary<string, string> { ["was_owner"] = isOwner ? "true" : "false" }), ct);

        await transaction.CommitAsync(ct);
        return RevokeOutcome.Revoked;
    }

    private static NpgsqlParameter Timestamp(string name, DateTimeOffset value) =>
        new(name, NpgsqlDbType.TimestampTz) { Value = value.UtcDateTime };

    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);
}