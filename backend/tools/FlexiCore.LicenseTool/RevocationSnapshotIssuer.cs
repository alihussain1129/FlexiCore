using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlexiCore.Licensing;

namespace FlexiCore.LicenseTool;

/// <summary>Creates a signed cumulative revocation snapshot with a signer authorized by the target public-key set.</summary>
public static class RevocationSnapshotIssuer
{
    public static byte[] Issue(
        IReadOnlyCollection<TrustedKey> authorizedKeys,
        ILicenseSigner signer,
        DateTimeOffset now,
        int validityDays = 7,
        byte[]? previousSnapshot = null,
        string? revokeLicenseId = null,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(authorizedKeys);
        ArgumentNullException.ThrowIfNull(signer);
        if (validityDays is < 1 or > 31) throw new LicenseToolException("Snapshot validity must be from 1 to 31 days.");
        var trusted = authorizedKeys.Where(k => string.Equals(k.KeyId, signer.KeyId, StringComparison.Ordinal)).ToArray();
        if (trusted.Length != 1 || !trusted[0].SubjectPublicKeyInfo.AsSpan().SequenceEqual(signer.SubjectPublicKeyInfo))
            throw new LicenseToolException("The signing key does not exactly match one authorized public key. Nothing was signed.", 2);

        var nowUtc = LicenseIssuer.Truncate(now.ToUniversalTime());
        var entries = new Dictionary<string, RevokedLicense>(StringComparer.Ordinal);
        if (previousSnapshot is not null)
        {
            var previous = new SignedRevocationListVerifier(authorizedKeys).Verify(previousSnapshot);
            if (!previous.IsValid || previous.List is null)
                throw new LicenseToolException("The previous revocation snapshot is invalid under the supplied trust set.", 2);
            if (previous.List.IssuedAt >= nowUtc)
                throw new LicenseToolException("The system clock has not advanced beyond the previous snapshot. Nothing was signed.", 2);
            foreach (var entry in previous.List.RevokedLicenses) entries.Add(entry.LicenseId, entry);
        }

        if ((revokeLicenseId is null) != (reason is null))
            throw new LicenseToolException("A revocation requires both a license id and a reason.");
        if (revokeLicenseId is not null)
        {
            if (!ValidLicenseId(revokeLicenseId)) throw new LicenseToolException("The license id is invalid.");
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500 || reason.Any(char.IsControl))
                throw new LicenseToolException("The revocation reason must be 1 to 500 printable characters.");
            if (entries.ContainsKey(revokeLicenseId))
                throw new LicenseToolException("That license is already revoked in the previous snapshot.");
            entries.Add(revokeLicenseId, new RevokedLicense(revokeLicenseId, nowUtc, reason.Trim()));
        }
        if (entries.Count > 20_000) throw new LicenseToolException("The snapshot exceeds the 20,000-entry limit.");

        var payload = BuildPayload(Guid.NewGuid().ToString("N"), nowUtc, nowUtc.AddDays(validityDays),
            entries.Values.OrderBy(x => x.LicenseId, StringComparer.Ordinal));
        var signature = signer.Sign(payload);
        if (signature.Length != LicenseLimits.SignatureBytes)
            throw new LicenseToolException("The signer returned an invalid ES256 signature length.", 2);
        var file = BuildEnvelope(signer.KeyId, payload, signature);
        var verified = new SignedRevocationListVerifier(authorizedKeys).Verify(file);
        if (!verified.IsValid || verified.List is null || verified.List.IssuedAt != nowUtc
            || verified.List.RevokedLicenses.Count != entries.Count)
            throw new LicenseToolException("The generated revocation snapshot failed its trust-set self-check.", 2);
        return file;
    }

    private static bool ValidLicenseId(string value) => value.Length is > 0 and <= LicenseLimits.MaxLicenseIdLength
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

    private static byte[] BuildPayload(string listId, DateTimeOffset issuedAt, DateTimeOffset nextUpdate,
        IEnumerable<RevokedLicense> entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("listId", listId);
            writer.WriteString("issuedAt", LicenseIssuer.FormatTimestamp(issuedAt));
            writer.WriteString("nextUpdate", LicenseIssuer.FormatTimestamp(nextUpdate));
            writer.WriteStartArray("revokedLicenses");
            foreach (var entry in entries)
            {
                writer.WriteStartObject();
                writer.WriteString("licenseId", entry.LicenseId);
                writer.WriteString("revokedAt", LicenseIssuer.FormatTimestamp(entry.RevokedAt));
                writer.WriteString("reason", entry.Reason);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static byte[] BuildEnvelope(string keyId, byte[] payload, byte[] signature)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("keyId", keyId);
            writer.WriteString("payload", Base64Url(payload));
            writer.WriteString("signature", Base64Url(signature));
            writer.WriteEndObject();
        }
        var result = stream.ToArray();
        if (result.Length > SignedRevocationListVerifier.MaxFileBytes)
            throw new LicenseToolException("The signed revocation snapshot exceeds the file size limit.");
        return result;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
