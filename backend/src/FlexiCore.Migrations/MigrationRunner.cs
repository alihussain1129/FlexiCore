using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace FlexiCore.Migrations;

public sealed record MigrationScript(string Version, string Sql)
{
    /// <summary>SHA-256 of the script text, insensitive to CRLF vs LF.</summary>
    public string Checksum =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Sql.ReplaceLineEndings("\n"))));
}

/// <summary>
/// Applies forward-only SQL migrations. Guarantees: one runner at a time (advisory lock),
/// each script atomic with its journal row, applied scripts immutable, and a database that is
/// ahead of this build is refused.
/// </summary>
public sealed class MigrationRunner(string connectionString, Action<string>? log = null)
{
    private const long AdvisoryLockKey = 7_246_100_001;

    public static IReadOnlyList<MigrationScript> LoadEmbeddedScripts()
    {
        var assembly = typeof(MigrationRunner).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("Scripts/", StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n =>
            {
                using var stream = assembly.GetManifestResourceStream(n)!;
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return new MigrationScript(Path.GetFileNameWithoutExtension(n), reader.ReadToEnd());
            })
            .ToList();
    }

    /// <returns>The number of migrations applied by this call.</returns>
    public async Task<int> RunAsync(IReadOnlyList<MigrationScript>? scripts = null, CancellationToken ct = default)
    {
        scripts ??= LoadEmbeddedScripts();
        var ordered = scripts.OrderBy(s => s.Version, StringComparer.Ordinal).ToList();
        if (ordered.Select(s => s.Version).Distinct(StringComparer.Ordinal).Count() != ordered.Count)
            throw new InvalidOperationException("Duplicate migration versions.");

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using (var lockCmd = new NpgsqlCommand("select pg_advisory_lock(@k)", conn))
        {
            lockCmd.Parameters.AddWithValue("k", AdvisoryLockKey);
            await lockCmd.ExecuteNonQueryAsync(ct);
        }

        try
        {
            await using (var create = new NpgsqlCommand(
                "create table if not exists public.schema_migrations (" +
                "version text primary key, checksum text not null, applied_at timestamptz not null default now())", conn))
            {
                await create.ExecuteNonQueryAsync(ct);
            }

            var applied = new Dictionary<string, string>(StringComparer.Ordinal);
            await using (var read = new NpgsqlCommand("select version, checksum from public.schema_migrations", conn))
            await using (var reader = await read.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                    applied[reader.GetString(0)] = reader.GetString(1);
            }

            var known = ordered.Select(s => s.Version).ToHashSet(StringComparer.Ordinal);
            var unknown = applied.Keys.FirstOrDefault(v => !known.Contains(v));
            if (unknown is not null)
                throw new InvalidOperationException(
                    $"The database has migration '{unknown}' applied, which this build does not contain. " +
                    "This build is older than the database; refusing to continue.");

            var count = 0;
            foreach (var script in ordered)
            {
                if (applied.TryGetValue(script.Version, out var existing))
                {
                    if (!string.Equals(existing, script.Checksum, StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            $"Migration '{script.Version}' was changed after it was applied. " +
                            "Migrations are immutable; add a new migration instead.");
                    continue;
                }

                await using var tx = await conn.BeginTransactionAsync(ct);
                await using (var run = new NpgsqlCommand(script.Sql, conn, tx))
                    await run.ExecuteNonQueryAsync(ct);
                await using (var journal = new NpgsqlCommand(
                    "insert into public.schema_migrations (version, checksum) values (@v, @c)", conn, tx))
                {
                    journal.Parameters.AddWithValue("v", script.Version);
                    journal.Parameters.AddWithValue("c", script.Checksum);
                    await journal.ExecuteNonQueryAsync(ct);
                }
                await tx.CommitAsync(ct);
                log?.Invoke($"Applied {script.Version}");
                count++;
            }
            return count;
        }
        finally
        {
            try
            {
                await using var unlock = new NpgsqlCommand("select pg_advisory_unlock(@k)", conn);
                unlock.Parameters.AddWithValue("k", AdvisoryLockKey);
                await unlock.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch
            {
                // Connection already broken; the lock dies with the session.
            }
        }
    }
}