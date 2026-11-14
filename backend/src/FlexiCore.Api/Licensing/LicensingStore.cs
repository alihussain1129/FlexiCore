using System.Security.Cryptography;
using System.Text.Json;
using FlexiCore.Licensing;
using FlexiCore.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace FlexiCore.Api.Licensing;

public enum LicensingWriteOutcome { Created, Unchanged, Conflict, Forbidden, Invalid }
public sealed record EntitlementAccess(bool Allowed, string? Reason, int? MaxUsers, int? MaxDevices, int? MaxCompanies, int? MaxBranches);
public sealed record SyncLicense(string LicenseId, byte[] SignedLicense, string Sha256, DateTimeOffset IssuedAt, string? PredecessorLicenseId);
public sealed record SyncActivation(string LicenseId, string InstallationId, string EventType, DateTimeOffset OccurredAt, string? PreviousInstallationId);
public sealed record SyncRenewal(string PreviousLicenseId, string RenewedLicenseId, DateTimeOffset AuthorizedAt, string Reason);
public sealed record SyncEntitlement(string LicenseId, int Revision, byte[] SignedArtifact, string Sha256, DateTimeOffset ImportedAt);
public sealed record HostedLicensingSyncBundle(DateTimeOffset GeneratedAt, bool IsComplete, string OfflinePolicy,
    IReadOnlyList<SyncLicense> Licenses, IReadOnlyList<SyncActivation> Activations,
    IReadOnlyList<SyncRenewal> Renewals, byte[]? RevocationSnapshot, DateTimeOffset? RevocationNextUpdate,
    IReadOnlyList<SyncEntitlement> Entitlements);

/// <summary>Tenant-scoped PostgreSQL persistence for hosted licensing lifecycle records.</summary>
public sealed class LicensingStore(ITenantConnectionFactory connections, TimeProvider time)
{
    public async Task<LicensingWriteOutcome> RegisterLicenseAsync(
        TenantContext context, Guid organizationId, Guid actorId, Guid requestId, string? installationId,
        byte[] signedLicense, string keyId, LicensePayload license, CancellationToken ct)
    {
        if (organizationId == Guid.Empty || actorId != context.UserId || requestId == Guid.Empty
            || installationId is not null && !InstallationIds.IsValid(installationId)
            || signedLicense.Length > LicenseLimits.MaxFileBytes)
            return LicensingWriteOutcome.Invalid;
        await using var scope = await connections.OpenAsync(context, ct);
        await LockOrganization(scope, organizationId, ct);
        var org = await ReadOrganizationAsync(scope, organizationId, ct);
        if (org is null) return LicensingWriteOutcome.Forbidden;
        if (!string.Equals(org.Value.Name.Trim(), license.Licensee.Trim(), StringComparison.OrdinalIgnoreCase))
            return LicensingWriteOutcome.Invalid;

        await using (var revokedBeforeRegistration = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM licensing.pending_revocations
              WHERE organization_id=@org AND license_id=@license)
            """, scope.Connection, scope.Transaction))
        {
            revokedBeforeRegistration.Parameters.AddWithValue("org", organizationId);
            revokedBeforeRegistration.Parameters.AddWithValue("license", license.LicenseId);
            if (Convert.ToBoolean(await revokedBeforeRegistration.ExecuteScalarAsync(ct)))
                return LicensingWriteOutcome.Forbidden;
        }

        var hash = Hash(signedLicense);
        await using (var idempotency = new NpgsqlCommand("""
            SELECT operation, request_sha256, result_id FROM licensing.idempotency_requests
            WHERE organization_id=@org AND request_id=@request
            """, scope.Connection, scope.Transaction))
        {
            idempotency.Parameters.AddWithValue("org", organizationId); idempotency.Parameters.AddWithValue("request", requestId);
            await using var reader = await idempotency.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var same = reader.GetString(0) == "license.import" && reader.GetString(1) == hash
                    && reader.GetString(2) == license.LicenseId;
                await reader.DisposeAsync();
                if (!same) return LicensingWriteOutcome.Conflict;
                await scope.CommitAsync(ct); return LicensingWriteOutcome.Unchanged;
            }
        }
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO licensing.licenses
              (license_id, organization_id, product, licensee, signing_key_id, signed_license,
               signed_license_sha256, issued_at, not_before, expires_at, grace_days, installation_id,
               max_installations, recorded_by)
            VALUES (@id, @org, @product, @licensee, @key, @bytes, @hash, @issued, @not_before,
               @expires, @grace, @installation, @max_installations, @actor)
            ON CONFLICT (license_id) DO NOTHING
            """, scope.Connection, scope.Transaction))
        {
            AddLicenseParameters(insert, organizationId, actorId, signedLicense, keyId, license, installationId, hash);
            if (await insert.ExecuteNonQueryAsync(ct) == 0)
            {
                await using var existing = new NpgsqlCommand(
                    "SELECT organization_id, signed_license_sha256 FROM licensing.licenses WHERE license_id=@id",
                    scope.Connection, scope.Transaction);
                existing.Parameters.AddWithValue("id", license.LicenseId);
                await using var reader = await existing.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) return LicensingWriteOutcome.Conflict;
                var same = reader.GetGuid(0) == organizationId && reader.GetString(1) == hash;
                await reader.DisposeAsync();
                if (!same) return LicensingWriteOutcome.Conflict;
                await SaveIdempotencyAsync(scope, organizationId, actorId, requestId, hash, license.LicenseId, ct);
                await scope.CommitAsync(ct);
                return LicensingWriteOutcome.Unchanged;
            }
        }
        await SaveIdempotencyAsync(scope, organizationId, actorId, requestId, hash, license.LicenseId, ct);
        await RecordAuditAsync(scope, actorId, organizationId, "licensing.license.import", license.LicenseId, requestId, ct);
        await scope.CommitAsync(ct);
        return LicensingWriteOutcome.Created;
    }

    public async Task<LicensingWriteOutcome> RecordActivationAsync(
        TenantContext context, Guid organizationId, Guid actorId, Guid requestId, string licenseId,
        string installationId, string eventType, string? previousInstallationId, CancellationToken ct)
    {
        if (requestId == Guid.Empty || !InstallationIds.IsValid(installationId)
            || previousInstallationId is not null && !InstallationIds.IsValid(previousInstallationId)
            || eventType is not ("activated" or "reactivated" or "transferred" or "deactivated")
            || eventType == "transferred" && previousInstallationId == installationId)
            return LicensingWriteOutcome.Invalid;
        await using var scope = await connections.OpenAsync(context, ct);
        await LockOrganization(scope, organizationId, ct);
        await using (var replay = new NpgsqlCommand("""
            SELECT license_id, installation_id, event_type, previous_installation_id
            FROM licensing.activation_events WHERE organization_id=@org AND request_id=@request
            """, scope.Connection, scope.Transaction))
        {
            replay.Parameters.AddWithValue("org", organizationId); replay.Parameters.AddWithValue("request", requestId);
            await using var reader = await replay.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var same = reader.GetString(0) == licenseId && reader.GetString(1) == installationId
                    && reader.GetString(2) == eventType
                    && (reader.IsDBNull(3) ? null : reader.GetString(3)) == previousInstallationId;
                await reader.DisposeAsync();
                if (!same) return LicensingWriteOutcome.Conflict;
                await scope.CommitAsync(ct); return LicensingWriteOutcome.Unchanged;
            }
        }
        if (eventType != "deactivated")
        {
            await using var eligible = new NpgsqlCommand("""
                SELECT installation_id, LEAST(l.max_installations, COALESCE((
                    SELECT min(e.max_devices) FROM licensing.entitlements e
                    WHERE e.organization_id=l.organization_id AND e.license_id=l.license_id
                      AND e.revision=(SELECT max(r.revision) FROM licensing.entitlement_revisions r
                        WHERE r.organization_id=l.organization_id AND r.license_id=l.license_id)
                      AND e.max_devices IS NOT NULL), l.max_installations))
                FROM licensing.licenses l
                WHERE l.organization_id=@org AND l.license_id=@license AND l.lifecycle_state='active'
                  AND l.not_before <= @now
                  AND (l.expires_at IS NULL OR @now <= l.expires_at + l.grace_days * interval '1 day')
                  AND (l.installation_id IS NULL OR l.installation_id=@installation)
                  AND NOT EXISTS (SELECT 1 FROM licensing.revocations rv
                    WHERE rv.organization_id=l.organization_id AND rv.license_id=l.license_id)
                  AND NOT EXISTS (SELECT 1 FROM licensing.renewals rn
                    WHERE rn.organization_id=l.organization_id AND rn.previous_license_id=l.license_id)
                  AND (SELECT s.next_update FROM licensing.revocation_snapshots s
                    WHERE s.organization_id=l.organization_id ORDER BY s.issued_at DESC LIMIT 1) > @now
                """, scope.Connection, scope.Transaction);
            eligible.Parameters.AddWithValue("org", organizationId); eligible.Parameters.AddWithValue("license", licenseId);
            eligible.Parameters.AddWithValue("installation", installationId); eligible.Parameters.AddWithValue("now", time.GetUtcNow());
            await using var reader = await eligible.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return LicensingWriteOutcome.Forbidden;
            var maxInstallations = reader.GetInt32(1);
            await reader.DisposeAsync();
            await using var count = new NpgsqlCommand("""
                WITH latest AS (
                  SELECT installation_id, event_type,
                    row_number() OVER (PARTITION BY installation_id ORDER BY occurred_at DESC, id DESC) AS n
                  FROM (
                    SELECT installation_id, event_type, occurred_at, id, previous_installation_id
                    FROM licensing.activation_events
                    WHERE organization_id=@org AND license_id=@license
                    UNION ALL
                    SELECT previous_installation_id, 'deactivated', occurred_at, id, NULL::text
                    FROM licensing.activation_events
                    WHERE organization_id=@org AND license_id=@license AND event_type='transferred'
                      AND previous_installation_id IS NOT NULL
                  ) events
                )
                SELECT count(*) FROM latest
                WHERE n=1 AND event_type <> 'deactivated' AND installation_id <> @installation
                  AND (@type <> 'transferred' OR installation_id <> @previous)
                """, scope.Connection, scope.Transaction);
            count.Parameters.AddWithValue("org", organizationId); count.Parameters.AddWithValue("license", licenseId);
            count.Parameters.AddWithValue("installation", installationId);
            count.Parameters.AddWithValue("type", eventType);
            count.Parameters.Add(new NpgsqlParameter("previous", NpgsqlDbType.Text) { Value = (object?)previousInstallationId ?? DBNull.Value });
            if (Convert.ToInt32(await count.ExecuteScalarAsync(ct)) >= maxInstallations)
                return LicensingWriteOutcome.Invalid;
        }
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO licensing.activation_events
              (organization_id, license_id, installation_id, event_type, occurred_at, authorized_by,
               request_id, previous_installation_id)
            SELECT @org, @license, @installation, @type, @now, @actor, @request, @previous
            WHERE EXISTS (SELECT 1 FROM licensing.licenses l WHERE l.organization_id=@org AND l.license_id=@license)
            ON CONFLICT (organization_id, request_id) DO NOTHING
            """, scope.Connection, scope.Transaction);
        cmd.Parameters.AddWithValue("org", organizationId); cmd.Parameters.AddWithValue("license", licenseId);
        cmd.Parameters.AddWithValue("installation", installationId); cmd.Parameters.AddWithValue("type", eventType);
        cmd.Parameters.AddWithValue("now", time.GetUtcNow()); cmd.Parameters.AddWithValue("actor", actorId);
        cmd.Parameters.AddWithValue("request", requestId);
        cmd.Parameters.Add(new NpgsqlParameter("previous", NpgsqlDbType.Text) { Value = (object?)previousInstallationId ?? DBNull.Value });
        var inserted = await cmd.ExecuteNonQueryAsync(ct);
        if (inserted == 0)
        {
            await using var check = new NpgsqlCommand("SELECT license_id, installation_id, event_type, previous_installation_id FROM licensing.activation_events WHERE organization_id=@org AND request_id=@request", scope.Connection, scope.Transaction);
            check.Parameters.AddWithValue("org", organizationId); check.Parameters.AddWithValue("request", requestId);
            await using var reader = await check.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return LicensingWriteOutcome.Forbidden;
            var same = reader.GetString(0) == licenseId && reader.GetString(1) == installationId
                && reader.GetString(2) == eventType
                && (reader.IsDBNull(3) ? null : reader.GetString(3)) == previousInstallationId;
            await reader.DisposeAsync();
            if (!same) return LicensingWriteOutcome.Conflict;
            await scope.CommitAsync(ct); return LicensingWriteOutcome.Unchanged;
        }
        await RecordAuditAsync(scope, actorId, organizationId, "licensing.activation." + eventType, licenseId, requestId, ct);
        await scope.CommitAsync(ct); return LicensingWriteOutcome.Created;
    }

    public async Task<LicensingWriteOutcome> RecordRenewalAsync(
        TenantContext context, Guid organizationId, Guid actorId, Guid requestId, string reason,
        byte[] signedLicense, string keyId, LicensePayload renewed, string predecessorLicenseId,
        CancellationToken ct)
    {
        if (requestId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000
            || signedLicense.Length > LicenseLimits.MaxFileBytes || predecessorLicenseId == renewed.LicenseId)
            return LicensingWriteOutcome.Invalid;
        await using var scope = await connections.OpenAsync(context, ct);
        await LockOrganization(scope, organizationId, ct);
        var org = await ReadOrganizationAsync(scope, organizationId, ct);
        if (org is null) return LicensingWriteOutcome.Forbidden;
        if (!string.Equals(org.Value.Name.Trim(), renewed.Licensee.Trim(), StringComparison.OrdinalIgnoreCase))
            return LicensingWriteOutcome.Invalid;

        await using (var duplicate = new NpgsqlCommand("""
            SELECT r.previous_license_id, r.renewed_license_id, r.reason, l.signed_license_sha256
            FROM licensing.renewals r
            JOIN licensing.licenses l ON l.organization_id=r.organization_id AND l.license_id=r.renewed_license_id
            WHERE r.organization_id=@org AND r.request_id=@request
            """, scope.Connection, scope.Transaction))
        {
            duplicate.Parameters.AddWithValue("org", organizationId); duplicate.Parameters.AddWithValue("request", requestId);
            await using var reader = await duplicate.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var same = reader.GetString(0) == predecessorLicenseId && reader.GetString(1) == renewed.LicenseId
                    && reader.GetString(2) == reason.Trim() && reader.GetString(3) == Hash(signedLicense);
                await reader.DisposeAsync();
                if (!same) return LicensingWriteOutcome.Conflict;
                await scope.CommitAsync(ct); return LicensingWriteOutcome.Unchanged;
            }
        }
        await using (var successor = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM licensing.licenses WHERE organization_id=@org AND license_id=@new_id)
                OR EXISTS (SELECT 1 FROM licensing.renewals WHERE organization_id=@org AND previous_license_id=@previous)
            """, scope.Connection, scope.Transaction))
        {
            successor.Parameters.AddWithValue("org", organizationId); successor.Parameters.AddWithValue("new_id", renewed.LicenseId);
            successor.Parameters.AddWithValue("previous", predecessorLicenseId);
            if (Convert.ToBoolean(await successor.ExecuteScalarAsync(ct))) return LicensingWriteOutcome.Conflict;
        }
        // A signed snapshot may already have revoked the successor id before this renewal arrived.
        // Registration enforces that through pending_revocations; renewal must enforce it as well.
        await using (var revokedSuccessor = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM licensing.pending_revocations
              WHERE organization_id=@org AND license_id=@license)
            """, scope.Connection, scope.Transaction))
        {
            revokedSuccessor.Parameters.AddWithValue("org", organizationId);
            revokedSuccessor.Parameters.AddWithValue("license", renewed.LicenseId);
            if (Convert.ToBoolean(await revokedSuccessor.ExecuteScalarAsync(ct)))
                return LicensingWriteOutcome.Forbidden;
        }
        await using (var predecessor = new NpgsqlCommand("""
            SELECT licensee, issued_at, installation_id FROM licensing.licenses l
            WHERE l.organization_id=@org AND l.license_id=@id AND l.lifecycle_state='active'
              AND NOT EXISTS (SELECT 1 FROM licensing.revocations r WHERE r.organization_id=l.organization_id AND r.license_id=l.license_id)
              AND NOT EXISTS (SELECT 1 FROM licensing.renewals rn WHERE rn.organization_id=l.organization_id AND rn.previous_license_id=l.license_id)
              AND (SELECT s.next_update FROM licensing.revocation_snapshots s
                   WHERE s.organization_id=l.organization_id ORDER BY s.issued_at DESC LIMIT 1) > now()
            """, scope.Connection, scope.Transaction))
        {
            predecessor.Parameters.AddWithValue("org", organizationId); predecessor.Parameters.AddWithValue("id", predecessorLicenseId);
            await using var reader = await predecessor.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return LicensingWriteOutcome.Forbidden;
            var priorLicensee = reader.GetString(0); var priorIssued = reader.GetFieldValue<DateTimeOffset>(1);
            var priorInstallation = reader.IsDBNull(2) ? null : reader.GetString(2);
            if (!string.Equals(priorLicensee, renewed.Licensee, StringComparison.OrdinalIgnoreCase)
                || renewed.IssuedAt <= priorIssued
                || renewed.InstallationId != priorInstallation)
                return LicensingWriteOutcome.Invalid;
        }
        var hash = Hash(signedLicense);
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO licensing.licenses
              (license_id, organization_id, product, licensee, signing_key_id, signed_license,
               signed_license_sha256, issued_at, not_before, expires_at, grace_days, installation_id,
               max_installations, predecessor_license_id, recorded_by)
            VALUES (@id, @org, @product, @licensee, @key, @bytes, @hash, @issued, @not_before,
               @expires, @grace, @installation, @max_installations, @predecessor, @actor)
            """, scope.Connection, scope.Transaction))
        {
            AddLicenseParameters(insert, organizationId, actorId, signedLicense, keyId, renewed,
                renewed.InstallationId, hash);
            insert.Parameters.AddWithValue("predecessor", predecessorLicenseId);
            await insert.ExecuteNonQueryAsync(ct);
        }
        await using (var record = new NpgsqlCommand("""
            INSERT INTO licensing.renewals
              (organization_id, previous_license_id, renewed_license_id, authorized_by, authorized_at, request_id, reason)
            VALUES (@org, @previous, @renewed, @actor, @now, @request, @reason)
            """, scope.Connection, scope.Transaction))
        {
            record.Parameters.AddWithValue("org", organizationId); record.Parameters.AddWithValue("previous", predecessorLicenseId);
            record.Parameters.AddWithValue("renewed", renewed.LicenseId); record.Parameters.AddWithValue("actor", actorId);
            record.Parameters.AddWithValue("now", time.GetUtcNow()); record.Parameters.AddWithValue("request", requestId);
            record.Parameters.AddWithValue("reason", reason.Trim()); await record.ExecuteNonQueryAsync(ct);
        }
        await RecordAuditAsync(scope, actorId, organizationId, "licensing.license.renew", renewed.LicenseId, requestId, ct);
        await scope.CommitAsync(ct); return LicensingWriteOutcome.Created;
    }

    public async Task<LicensingWriteOutcome> ImportRevocationSnapshotAsync(
        TenantContext context, Guid organizationId, Guid actorId, byte[] artifact,
        SignedRevocationList snapshot, string keyId, CancellationToken ct)
    {
        if (!Guid.TryParseExact(snapshot.ListId, "N", out var listId) || artifact.Length > SignedRevocationListVerifier.MaxFileBytes)
            return LicensingWriteOutcome.Invalid;
        if (snapshot.IssuedAt > time.GetUtcNow().AddMinutes(5) || snapshot.NextUpdate <= time.GetUtcNow())
            return LicensingWriteOutcome.Invalid;
        await using var scope = await connections.OpenAsync(context, ct);
        await LockOrganization(scope, organizationId, ct);
        var hash = Hash(artifact);
        await using (var priorId = new NpgsqlCommand(
            "SELECT snapshot_sha256 FROM licensing.revocation_snapshots WHERE organization_id=@org AND list_id=@id",
            scope.Connection, scope.Transaction))
        {
            priorId.Parameters.AddWithValue("org", organizationId); priorId.Parameters.AddWithValue("id", listId);
            var existing = await priorId.ExecuteScalarAsync(ct) as string;
            if (existing is not null)
            {
                if (existing != hash) return LicensingWriteOutcome.Conflict;
                await scope.CommitAsync(ct); return LicensingWriteOutcome.Unchanged;
            }
        }
        await using (var latest = new NpgsqlCommand(
            "SELECT issued_at FROM licensing.revocation_snapshots WHERE organization_id=@org ORDER BY issued_at DESC LIMIT 1",
            scope.Connection, scope.Transaction))
        {
            latest.Parameters.AddWithValue("org", organizationId);
            await using var reader = await latest.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct) && snapshot.IssuedAt <= reader.GetFieldValue<DateTimeOffset>(0))
                return LicensingWriteOutcome.Conflict;
        }
        var incoming = snapshot.RevokedLicenses.ToDictionary(r => r.LicenseId, StringComparer.Ordinal);
        await using (var oldRevocations = new NpgsqlCommand(
            "SELECT license_id, revoked_at, reason FROM licensing.revocations WHERE organization_id=@org",
            scope.Connection, scope.Transaction))
        {
            oldRevocations.Parameters.AddWithValue("org", organizationId);
            await using var reader = await oldRevocations.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (!incoming.TryGetValue(reader.GetString(0), out var current)
                    || current.RevokedAt != reader.GetFieldValue<DateTimeOffset>(1)
                    || current.Reason != reader.GetString(2))
                    return LicensingWriteOutcome.Conflict;
            }
        }
        await using (var oldPending = new NpgsqlCommand(
            "SELECT license_id, revoked_at, reason FROM licensing.pending_revocations WHERE organization_id=@org",
            scope.Connection, scope.Transaction))
        {
            oldPending.Parameters.AddWithValue("org", organizationId);
            await using var reader = await oldPending.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (!incoming.TryGetValue(reader.GetString(0), out var current)
                    || current.RevokedAt != reader.GetFieldValue<DateTimeOffset>(1)
                    || current.Reason != reader.GetString(2))
                    return LicensingWriteOutcome.Conflict;
            }
        }
        await using (var save = new NpgsqlCommand("""
            INSERT INTO licensing.revocation_snapshots
              (id, organization_id, list_id, signing_key_id, issued_at, next_update, signed_snapshot, snapshot_sha256, imported_by)
            VALUES (@id, @org, @id, @key, @issued, @next, @bytes, @hash, @actor)
            """, scope.Connection, scope.Transaction))
        {
            save.Parameters.AddWithValue("org", organizationId); save.Parameters.AddWithValue("id", listId);
            save.Parameters.AddWithValue("key", keyId); save.Parameters.AddWithValue("issued", snapshot.IssuedAt);
            save.Parameters.AddWithValue("next", snapshot.NextUpdate); save.Parameters.AddWithValue("bytes", artifact);
            save.Parameters.AddWithValue("hash", hash); save.Parameters.AddWithValue("actor", actorId);
            await save.ExecuteNonQueryAsync(ct);
        }
        foreach (var revoked in snapshot.RevokedLicenses)
        {
            await using var knownLicense = new NpgsqlCommand(
                "SELECT EXISTS (SELECT 1 FROM licensing.licenses WHERE organization_id=@org AND license_id=@license)",
                scope.Connection, scope.Transaction);
            knownLicense.Parameters.AddWithValue("org", organizationId);
            knownLicense.Parameters.AddWithValue("license", revoked.LicenseId);
            var exists = Convert.ToBoolean(await knownLicense.ExecuteScalarAsync(ct));
            await using var insert = new NpgsqlCommand(exists ? """
                INSERT INTO licensing.revocations(organization_id, license_id, snapshot_id, revoked_at, reason, authorized_by)
                VALUES (@org, @license, @snapshot, @at, @reason, @actor)
                ON CONFLICT (organization_id, license_id) DO NOTHING
                """ : """
                INSERT INTO licensing.pending_revocations(organization_id, license_id, snapshot_id, revoked_at, reason, authorized_by)
                VALUES (@org, @license, @snapshot, @at, @reason, @actor)
                ON CONFLICT (organization_id, license_id) DO NOTHING
                """, scope.Connection, scope.Transaction);
            insert.Parameters.AddWithValue("org", organizationId); insert.Parameters.AddWithValue("license", revoked.LicenseId);
            insert.Parameters.AddWithValue("snapshot", listId); insert.Parameters.AddWithValue("at", revoked.RevokedAt);
            insert.Parameters.AddWithValue("reason", revoked.Reason); insert.Parameters.AddWithValue("actor", actorId);
            await insert.ExecuteNonQueryAsync(ct);
        }
        await RecordAuditAsync(scope, actorId, organizationId, "licensing.revocations.import", listId.ToString("D"), null, ct);
        await scope.CommitAsync(ct); return LicensingWriteOutcome.Created;
    }

    public async Task<LicensingWriteOutcome> ImportEntitlementAsync(
        TenantContext context, Guid organizationId, Guid actorId, byte[] artifact,
        ModuleEntitlementVerification verification, CancellationToken ct)
    {
        if (!verification.IsValid || verification.Payload is not { } payload || verification.KeyId is null
            || payload.OrganizationId != organizationId || artifact.Length > SignedModuleEntitlementVerifier.MaxFileBytes)
            return LicensingWriteOutcome.Invalid;
        await using var scope = await connections.OpenAsync(context, ct);
        await LockOrganization(scope, organizationId, ct);
        var org = await ReadOrganizationAsync(scope, organizationId, ct);
        if (org is null || !string.Equals(org.Value.Slug, payload.CustomerId, StringComparison.Ordinal))
            return LicensingWriteOutcome.Forbidden;

        await using (var license = new NpgsqlCommand("""
            SELECT licensee, lifecycle_state, not_before, expires_at, grace_days
            FROM licensing.licenses WHERE organization_id=@org AND license_id=@license
              AND NOT EXISTS (SELECT 1 FROM licensing.revocations rv
                WHERE rv.organization_id=licensing.licenses.organization_id AND rv.license_id=licensing.licenses.license_id)
              AND NOT EXISTS (SELECT 1 FROM licensing.renewals rn
                WHERE rn.organization_id=licensing.licenses.organization_id AND rn.previous_license_id=licensing.licenses.license_id)
              AND (SELECT s.next_update FROM licensing.revocation_snapshots s
                WHERE s.organization_id=licensing.licenses.organization_id ORDER BY s.issued_at DESC LIMIT 1) > now()
            """, scope.Connection, scope.Transaction))
        {
            license.Parameters.AddWithValue("org", organizationId); license.Parameters.AddWithValue("license", payload.LicenseId);
            await using var reader = await license.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return LicensingWriteOutcome.Forbidden;
            var licensee = reader.GetString(0); var state = reader.GetString(1); var notBefore = reader.GetFieldValue<DateTimeOffset>(2);
            var expires = reader.IsDBNull(3) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(3);
            var grace = reader.GetInt32(4);
            if (!string.Equals(licensee, org.Value.Name, StringComparison.OrdinalIgnoreCase) || state != "active"
                || time.GetUtcNow() < notBefore || expires is { } expiry && time.GetUtcNow() > expiry.AddDays(grace))
                return LicensingWriteOutcome.Invalid;
        }
        var digest = Hash(artifact);
        await using (var existing = new NpgsqlCommand(
            "SELECT signed_entitlement_hash FROM licensing.entitlement_revisions WHERE organization_id=@org AND license_id=@license AND revision=@revision",
            scope.Connection, scope.Transaction))
        {
            existing.Parameters.AddWithValue("org", organizationId); existing.Parameters.AddWithValue("license", payload.LicenseId);
            existing.Parameters.AddWithValue("revision", payload.Revision);
            var prior = await existing.ExecuteScalarAsync(ct) as string;
            if (prior is not null)
            {
                if (prior != digest) return LicensingWriteOutcome.Conflict;
                await scope.CommitAsync(ct); return LicensingWriteOutcome.Unchanged;
            }
        }
        await using (var latest = new NpgsqlCommand(
            "SELECT COALESCE(MAX(revision),0) FROM licensing.entitlement_revisions WHERE organization_id=@org AND license_id=@license",
            scope.Connection, scope.Transaction))
        {
            latest.Parameters.AddWithValue("org", organizationId); latest.Parameters.AddWithValue("license", payload.LicenseId);
            if (payload.Revision <= Convert.ToInt32(await latest.ExecuteScalarAsync(ct))) return LicensingWriteOutcome.Conflict;
        }
        await using (var revision = new NpgsqlCommand("""
            INSERT INTO licensing.entitlement_revisions
              (organization_id, license_id, revision, signing_key_id, signed_entitlement, signed_entitlement_hash, imported_by)
            VALUES (@org, @license, @revision, @key, @bytes, @hash, @actor)
            """, scope.Connection, scope.Transaction))
        {
            revision.Parameters.AddWithValue("org", organizationId); revision.Parameters.AddWithValue("license", payload.LicenseId);
            revision.Parameters.AddWithValue("revision", payload.Revision); revision.Parameters.AddWithValue("key", verification.KeyId);
            revision.Parameters.AddWithValue("bytes", artifact); revision.Parameters.AddWithValue("hash", digest); revision.Parameters.AddWithValue("actor", actorId);
            await revision.ExecuteNonQueryAsync(ct);
        }
        foreach (var grant in payload.Modules)
        {
            await using var row = new NpgsqlCommand("""
                INSERT INTO licensing.entitlements
                  (organization_id, license_id, revision, module_key, feature_key, enabled, valid_from, valid_until,
                   max_users, max_devices, max_companies, max_branches, signed_entitlement_hash)
                VALUES (@org, @license, @revision, @module, @feature, @enabled, @from, @until,
                   @users, @devices, @companies, @branches, @hash)
                """, scope.Connection, scope.Transaction);
            row.Parameters.AddWithValue("org", organizationId); row.Parameters.AddWithValue("license", payload.LicenseId);
            row.Parameters.AddWithValue("revision", payload.Revision); row.Parameters.AddWithValue("module", grant.ModuleKey);
            row.Parameters.AddWithValue("feature", grant.FeatureKey); row.Parameters.AddWithValue("enabled", grant.Enabled);
            row.Parameters.AddWithValue("from", payload.ValidFrom);
            row.Parameters.Add(new NpgsqlParameter("until", NpgsqlDbType.TimestampTz) { Value = (object?)payload.ValidUntil ?? DBNull.Value });
            row.Parameters.Add(new NpgsqlParameter("users", NpgsqlDbType.Integer) { Value = (object?)grant.MaxUsers ?? DBNull.Value });
            row.Parameters.Add(new NpgsqlParameter("devices", NpgsqlDbType.Integer) { Value = (object?)grant.MaxDevices ?? DBNull.Value });
            row.Parameters.Add(new NpgsqlParameter("companies", NpgsqlDbType.Integer) { Value = (object?)grant.MaxCompanies ?? DBNull.Value });
            row.Parameters.Add(new NpgsqlParameter("branches", NpgsqlDbType.Integer) { Value = (object?)grant.MaxBranches ?? DBNull.Value });
            row.Parameters.AddWithValue("hash", digest);
            await row.ExecuteNonQueryAsync(ct);
        }
        await RecordAuditAsync(scope, actorId, organizationId, "licensing.entitlement.import", payload.LicenseId, null, ct);
        await scope.CommitAsync(ct); return LicensingWriteOutcome.Created;
    }

    public async Task<EntitlementAccess> CheckAccessAsync(
        TenantContext context, Guid organizationId, string moduleKey, string featureKey, CancellationToken ct)
    {
        await using var scope = await connections.OpenAsync(context, ct);
        return await CheckAccessAsync(scope, organizationId, moduleKey, featureKey, ct);
    }

    public async Task<EntitlementAccess> CheckAccessAsync(
        TenantScope scope, Guid organizationId, string moduleKey, string featureKey, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            WITH latest AS (
              SELECT r.license_id, r.revision
              FROM licensing.entitlement_revisions r
              JOIN licensing.licenses l ON l.organization_id=r.organization_id AND l.license_id=r.license_id
              WHERE r.organization_id=@org AND l.lifecycle_state='active'
                AND l.not_before <= now()
                AND (l.expires_at IS NULL OR now() <= l.expires_at + l.grace_days * interval '1 day')
                AND NOT EXISTS (SELECT 1 FROM licensing.revocations rv WHERE rv.organization_id=l.organization_id AND rv.license_id=l.license_id)
                AND NOT EXISTS (SELECT 1 FROM licensing.renewals rn WHERE rn.organization_id=l.organization_id AND rn.previous_license_id=l.license_id)
              AND (SELECT s.next_update FROM licensing.revocation_snapshots s
                   WHERE s.organization_id=l.organization_id ORDER BY s.issued_at DESC LIMIT 1) > now()
              ORDER BY r.imported_at DESC, l.issued_at DESC, r.license_id DESC LIMIT 1
            )
            SELECT e.enabled, e.max_users, e.max_devices, e.max_companies, e.max_branches
            FROM latest x
            JOIN licensing.entitlements e ON e.organization_id=@org AND e.license_id=x.license_id AND e.revision=x.revision
            WHERE e.module_key=@module AND e.feature_key IN (@feature, '*')
              AND e.valid_from <= now() AND (e.valid_until IS NULL OR now() < e.valid_until)
            ORDER BY (e.feature_key=@feature) DESC LIMIT 1
            """, scope.Connection, scope.Transaction);
        cmd.Parameters.AddWithValue("org", organizationId); cmd.Parameters.AddWithValue("module", moduleKey); cmd.Parameters.AddWithValue("feature", featureKey);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return new(false, "license_or_entitlement_unavailable", null, null, null, null);
        return new(reader.GetBoolean(0), reader.GetBoolean(0) ? null : "feature_not_licensed",
            reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetInt32(3), reader.IsDBNull(4) ? null : reader.GetInt32(4));
    }

    public async Task<HostedLicensingSyncBundle> GetSyncBundleAsync(TenantContext context, Guid organizationId, CancellationToken ct)
    {
        await using var scope = await connections.OpenAsync(context, ct);
        var licenses = new List<SyncLicense>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT license_id, signed_license, signed_license_sha256, issued_at, predecessor_license_id
            FROM licensing.licenses WHERE organization_id=@org ORDER BY issued_at DESC, license_id LIMIT 501
            """, scope.Connection, scope.Transaction))
        {
            cmd.Parameters.AddWithValue("org", organizationId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) licenses.Add(new(reader.GetString(0), reader.GetFieldValue<byte[]>(1),
                reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
        }
        var activations = new List<SyncActivation>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT license_id, installation_id, event_type, occurred_at, previous_installation_id
            FROM licensing.activation_events WHERE organization_id=@org ORDER BY occurred_at DESC, id DESC LIMIT 1001
            """, scope.Connection, scope.Transaction))
        {
            cmd.Parameters.AddWithValue("org", organizationId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) activations.Add(new(reader.GetString(0), reader.GetString(1),
                reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
        }
        var renewals = new List<SyncRenewal>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT previous_license_id, renewed_license_id, authorized_at, reason
            FROM licensing.renewals WHERE organization_id=@org ORDER BY authorized_at DESC, id DESC LIMIT 1001
            """, scope.Connection, scope.Transaction))
        {
            cmd.Parameters.AddWithValue("org", organizationId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) renewals.Add(new(reader.GetString(0), reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2), reader.GetString(3)));
        }
        byte[]? snapshot = null; DateTimeOffset? nextUpdate = null;
        await using (var cmd = new NpgsqlCommand("""
            SELECT signed_snapshot, next_update FROM licensing.revocation_snapshots
            WHERE organization_id=@org ORDER BY issued_at DESC LIMIT 1
            """, scope.Connection, scope.Transaction))
        {
            cmd.Parameters.AddWithValue("org", organizationId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct)) { snapshot = reader.GetFieldValue<byte[]>(0); nextUpdate = reader.GetFieldValue<DateTimeOffset>(1); }
        }
        var entitlements = new List<SyncEntitlement>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT license_id, revision, signed_entitlement, signed_entitlement_hash, imported_at
            FROM licensing.entitlement_revisions WHERE organization_id=@org ORDER BY imported_at DESC LIMIT 1001
            """, scope.Connection, scope.Transaction))
        {
            cmd.Parameters.AddWithValue("org", organizationId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) entitlements.Add(new(reader.GetString(0), reader.GetInt32(1),
                reader.GetFieldValue<byte[]>(2), reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4)));
        }
        await scope.CommitAsync(ct);
        var complete = licenses.Count <= 500 && activations.Count <= 1000 && renewals.Count <= 1000 && entitlements.Count <= 1000;
        return new(time.GetUtcNow(), complete,
            "Offline authorization is valid only while the signed license and the latest signed revocation snapshot remain within their validity windows.",
            licenses.Take(500).ToArray(), activations.Take(1000).ToArray(), renewals.Take(1000).ToArray(), snapshot,
            nextUpdate, entitlements.Take(1000).ToArray());
    }

    private static async Task<(string Name, string Slug)?> ReadOrganizationAsync(TenantScope scope, Guid organizationId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT name, slug FROM tenancy.organizations WHERE id=@org", scope.Connection, scope.Transaction);
        cmd.Parameters.AddWithValue("org", organizationId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? (reader.GetString(0), reader.GetString(1)) : null;
    }

    private static async Task LockOrganization(TenantScope scope, Guid organizationId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key, 581284))", scope.Connection, scope.Transaction);
        cmd.Parameters.AddWithValue("key", "licensing:" + organizationId.ToString("N"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void AddLicenseParameters(NpgsqlCommand cmd, Guid org, Guid actor, byte[] bytes, string key,
        LicensePayload p, string? installationId, string hash)
    {
        cmd.Parameters.AddWithValue("id", p.LicenseId); cmd.Parameters.AddWithValue("org", org);
        cmd.Parameters.AddWithValue("product", p.Product); cmd.Parameters.AddWithValue("licensee", p.Licensee);
        cmd.Parameters.AddWithValue("key", key); cmd.Parameters.AddWithValue("bytes", bytes); cmd.Parameters.AddWithValue("hash", hash);
        cmd.Parameters.AddWithValue("issued", p.IssuedAt); cmd.Parameters.AddWithValue("not_before", p.NotBefore);
        cmd.Parameters.Add(new NpgsqlParameter("expires", NpgsqlDbType.TimestampTz) { Value = (object?)p.ExpiresAt ?? DBNull.Value });
        cmd.Parameters.AddWithValue("grace", p.GraceDays);
        cmd.Parameters.Add(new NpgsqlParameter("installation", NpgsqlDbType.Text) { Value = (object?)installationId ?? DBNull.Value });
        cmd.Parameters.AddWithValue("max_installations", p.MaxInstallations); cmd.Parameters.AddWithValue("actor", actor);
    }

    private static async Task RecordAuditAsync(TenantScope scope, Guid actor, Guid org, string action, string target,
        Guid? request, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO audit.events(actor_user_id, tenant_id, action, target_type, target_id, outcome, correlation_id, metadata)
            VALUES (@actor, @org, @action, 'license', @target, 'success', NULLIF(current_setting('app.correlation_id', true), ''),
              jsonb_build_object('requestId', @request))
            """, scope.Connection, scope.Transaction);
        cmd.Parameters.AddWithValue("actor", actor); cmd.Parameters.AddWithValue("org", org);
        cmd.Parameters.AddWithValue("action", action); cmd.Parameters.AddWithValue("target", target);
        cmd.Parameters.Add(new NpgsqlParameter("request", NpgsqlDbType.Text) { Value = request?.ToString("D") ?? "" });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task SaveIdempotencyAsync(TenantScope scope, Guid organizationId, Guid actorId,
        Guid requestId, string hash, string resultId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO licensing.idempotency_requests
              (organization_id, request_id, operation, request_sha256, result_id, actor_user_id)
            VALUES (@org, @request, 'license.import', @hash, @result, @actor)
            """, scope.Connection, scope.Transaction);
        cmd.Parameters.AddWithValue("org", organizationId); cmd.Parameters.AddWithValue("request", requestId);
        cmd.Parameters.AddWithValue("hash", hash); cmd.Parameters.AddWithValue("result", resultId);
        cmd.Parameters.AddWithValue("actor", actorId); await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
