using System.Text.Json;
using FlexiCore.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace FlexiCore.Catalog.Data;

public sealed record CatalogAuditEvent(
    string Action, Guid ActorUserId, Guid CompanyId, string TargetType, Guid TargetId, string? CorrelationId, int Version);

/// <summary>
/// Writes the success audit row inside the same transaction as the catalog write, so the row cannot be
/// skipped and cannot exist without the change it describes. Refused writes roll back and write nothing.
/// </summary>
public sealed class CatalogAuditWriter
{
    private const string InsertSql =
        "insert into audit.events (actor_user_id, tenant_id, action, target_type, target_id, outcome, correlation_id, metadata) " +
        "values (@actor, @tenant, @action, @target_type, @target_id, 'success', @correlation_id, @metadata)";

    public async Task WriteSuccessAsync(TenantScope scope, CatalogAuditEvent e, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(InsertSql, scope.Connection, scope.Transaction);
        command.Parameters.Add(new NpgsqlParameter("actor", NpgsqlDbType.Uuid) { Value = e.ActorUserId });
        command.Parameters.Add(new NpgsqlParameter("tenant", NpgsqlDbType.Uuid) { Value = e.CompanyId });
        command.Parameters.Add(new NpgsqlParameter("action", NpgsqlDbType.Text) { Value = e.Action });
        command.Parameters.Add(new NpgsqlParameter("target_type", NpgsqlDbType.Text) { Value = e.TargetType });
        command.Parameters.Add(new NpgsqlParameter("target_id", NpgsqlDbType.Text) { Value = e.TargetId.ToString() });
        command.Parameters.Add(new NpgsqlParameter("correlation_id", NpgsqlDbType.Text) { Value = (object?)e.CorrelationId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("metadata", NpgsqlDbType.Jsonb)
        {
            Value = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["company_id"] = e.CompanyId.ToString(),
                ["version"] = e.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)
            })
        });
        await command.ExecuteNonQueryAsync(ct);
    }
}