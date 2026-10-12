using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace FlexiCore.Api.Health;

public sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("select 1");
            command.CommandTimeout = 3;
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            // The exception goes to logs only; the HTTP body stays a bare status word.
            return HealthCheckResult.Unhealthy("PostgreSQL is not reachable.", ex);
        }
    }
}