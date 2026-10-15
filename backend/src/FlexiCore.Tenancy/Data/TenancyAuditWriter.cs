using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace FlexiCore.Tenancy.Data;

/// <summary>Metadata must never contain passwords, tokens or other secrets.</summary>
public sealed record TenancyAuditEvent(
    string Action,
    string Outcome,
    Guid? ActorUserId = null,
    string? TargetType = null,
    string? TargetId = null,
    string? CorrelationId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>
/// Writes refusal audit rows for tenancy actions in their own short transaction, after the failed
/// operation has rolled back. Successes are audited inside the database functions instead.
/// This deliberately duplicates the single insert from Identity's AuditWriter, because Tenancy must not
/// depend on Identity. Move the writer to a shared project when a third module needs it.
/// </summary>
public sealed class TenancyAuditWriter(NpgsqlDataSource dataSource)
{
    private const string InsertSql =
        "insert into audit.events (actor_user_id, action, target_type, target_id, outcome, correlation_id, metadata) " +
        "values (@actor, @action, @target_type, @target_id, @outcome, @correlation_id, @metadata)";

    public async Task WriteAsync(TenancyAuditEvent e, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(InsertSql);
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
        await command.ExecuteNonQueryAsync(ct);
    }
}