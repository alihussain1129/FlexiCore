using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace FlexiCore.Identity.Data;

/// <summary>Metadata must never contain passwords, tokens or other secrets.</summary>
public sealed record AuditEvent(
    string Action,
    string Outcome,
    Guid? ActorUserId = null,
    string? TargetType = null,
    string? TargetId = null,
    string? CorrelationId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed class AuditWriter(NpgsqlDataSource dataSource)
{
    private const string InsertSql =
        "insert into audit.events (actor_user_id, action, target_type, target_id, outcome, correlation_id, metadata) " +
        "values (@actor, @action, @target_type, @target_id, @outcome, @correlation_id, @metadata)";

    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(InsertSql);
        Bind(command, auditEvent);
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Writes inside the caller's transaction so the audit row and the change commit or roll back together.</summary>
    public async Task WriteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, AuditEvent auditEvent, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(InsertSql, connection, transaction);
        Bind(command, auditEvent);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static void Bind(NpgsqlCommand command, AuditEvent e)
    {
        command.Parameters.Add(new NpgsqlParameter("actor", NpgsqlDbType.Uuid) { Value = (object?)e.ActorUserId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("action", NpgsqlDbType.Text) { Value = e.Action });
        command.Parameters.Add(new NpgsqlParameter("target_type", NpgsqlDbType.Text) { Value = (object?)e.TargetType ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("target_id", NpgsqlDbType.Text) { Value = (object?)e.TargetId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("outcome", NpgsqlDbType.Text) { Value = e.Outcome });
        command.Parameters.Add(new NpgsqlParameter("correlation_id", NpgsqlDbType.Text) { Value = (object?)e.CorrelationId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("metadata", NpgsqlDbType.Jsonb)
        {
            Value = JsonSerializer.Serialize(e.Metadata ?? new Dictionary<string, string>())
        });
    }
}