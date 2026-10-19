using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace FlexiCore.Devices.Data;

/// <summary>
/// Metadata must never contain device secrets, pairing codes, tokens or other secrets. ActorKind is
/// "device", "administrator" or "anonymous"; a device is not proof of which person acted.
/// Until the shared audit work, actor details live in metadata and the existing columns are unchanged.
/// </summary>
public sealed record DeviceAuditEvent(
    string Action,
    string Outcome,
    string? TargetId,
    string ActorKind,
    Guid? ActorDeviceId,
    string? CorrelationId,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed class DeviceAuditWriter(NpgsqlDataSource dataSource)
{
    private const string InsertSql =
        "insert into audit.events (action, target_type, target_id, outcome, correlation_id, metadata) " +
        "values (@action, 'device', @target_id, @outcome, @correlation_id, @metadata)";

    public async Task WriteAsync(DeviceAuditEvent auditEvent, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(InsertSql);
        Bind(command, auditEvent);
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Writes inside the caller's transaction so the audit row and the change commit or roll back together.</summary>
    public async Task WriteAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, DeviceAuditEvent auditEvent, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(InsertSql, connection, transaction);
        Bind(command, auditEvent);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static void Bind(NpgsqlCommand command, DeviceAuditEvent e)
    {
        var metadata = new Dictionary<string, string>(e.Metadata ?? new Dictionary<string, string>())
        {
            ["actor_kind"] = e.ActorKind,
        };
        if (e.ActorDeviceId is { } actor) metadata["actor_device_id"] = actor.ToString();

        command.Parameters.Add(new NpgsqlParameter("action", NpgsqlDbType.Text) { Value = e.Action });
        command.Parameters.Add(new NpgsqlParameter("target_id", NpgsqlDbType.Text) { Value = (object?)e.TargetId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("outcome", NpgsqlDbType.Text) { Value = e.Outcome });
        command.Parameters.Add(new NpgsqlParameter("correlation_id", NpgsqlDbType.Text) { Value = (object?)e.CorrelationId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("metadata", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(metadata) });
    }
}