using System.Text.Json;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Services;

public enum TermStatus { NotYetValid, Active, ActiveInGrace, Expired }

/// <summary>One line of the existing issue ledger. Status is computed from the recorded dates only.</summary>
public sealed record LedgerEntry(
    DateTimeOffset RecordedAt,
    string LicenseId,
    string Licensee,
    string KeyId,
    DateTimeOffset NotBefore,
    DateTimeOffset? ExpiresAt,
    int GraceDays,
    int MaxInstallations,
    string FileSha256)
{
    public TermStatus StatusAt(DateTimeOffset now)
    {
        if (now < NotBefore) return TermStatus.NotYetValid;
        if (ExpiresAt is null || now <= ExpiresAt.Value) return TermStatus.Active;
        if (now <= LicenseVerifier.GraceEnd(ExpiresAt.Value, GraceDays)) return TermStatus.ActiveInGrace;
        return TermStatus.Expired;
    }
}

public sealed record LedgerSnapshot(IReadOnlyList<LedgerEntry> Entries, int UnreadableLines, string? Problem);

public static class LedgerReader
{
    private const long MaxBytes = 50L * 1024 * 1024;

    public static LedgerSnapshot Read(string path)
    {
        var entries = new List<LedgerEntry>();
        var unreadable = 0;
        try
        {
            if (!File.Exists(path)) return new LedgerSnapshot(entries, 0, null);
            if (new FileInfo(path).Length > MaxBytes)
                return new LedgerSnapshot(entries, 0, "The ledger is larger than 50 MB and was not loaded.");

            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var entry = TryParse(line);
                if (entry is null) unreadable++;
                else entries.Add(entry);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new LedgerSnapshot(entries, unreadable, "The ledger cannot be read (" + ex.GetType().Name + ").");
        }
        return new LedgerSnapshot(entries, unreadable, null);
    }

    private static LedgerEntry? TryParse(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;

            if (!Text(r, "recordedAt", out var recordedText)
                || !LicenseIssuer.TryParseTimestamp(recordedText, out var recordedAt)) return null;
            if (!Text(r, "licenseId", out var licenseId)) return null;
            if (!Text(r, "licensee", out var licensee)) return null;
            if (!Text(r, "keyId", out var keyId)) return null;
            if (!Text(r, "notBefore", out var notBeforeText)
                || !LicenseIssuer.TryParseTimestamp(notBeforeText, out var notBefore)) return null;

            DateTimeOffset? expiresAt = null;
            if (!r.TryGetProperty("expiresAt", out var expiresElement)) return null;
            if (expiresElement.ValueKind == JsonValueKind.String)
            {
                if (!LicenseIssuer.TryParseTimestamp(expiresElement.GetString() ?? "", out var parsedExpiry)) return null;
                expiresAt = parsedExpiry;
            }
            else if (expiresElement.ValueKind != JsonValueKind.Null)
            {
                return null;
            }

            if (!Number(r, "graceDays", out var grace)) return null;
            if (!Number(r, "maxInstallations", out var installs)) return null;
            Text(r, "fileSha256", out var sha);

            return new LedgerEntry(recordedAt, licenseId, licensee, keyId, notBefore, expiresAt, grace, installs, sha);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool Text(JsonElement r, string name, out string value)
    {
        value = "";
        if (r.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String)
        {
            value = e.GetString() ?? "";
            return true;
        }
        return false;
    }

    private static bool Number(JsonElement r, string name, out int value)
    {
        value = 0;
        return r.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out value);
    }
}
