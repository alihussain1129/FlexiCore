using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlexiCore.Licensing;

namespace FlexiCore.LicenseTool;

/// <summary>
/// Append-only record of what was issued and renewed (one JSON line each). It holds customer names, so it must live
/// outside every git working tree. Signed revocation snapshots are stored separately.
/// </summary>
public static class IssueLedger
{
    public sealed record IssuedLicenseRecord(
        string LicenseId, string Licensee, string FileSha256, string? InstallationId, Guid? CustomerId,
        Guid? ActivationRequestId, string? IssuedBy, string? RenewedFromLicenseId);

    public static void Append(string path, LicenseRequest request, string keyId, byte[] licenseFile, DateTimeOffset recordedAt)
    {
        using var ledgerLock = LedgerFileLock.Acquire(path);
        Append(path, request, keyId, licenseFile, recordedAt, ledgerLock);
    }

    /// <summary>
    /// Appends while the caller holds the shared lock across its complete operation. The record is written with a
    /// single write at the end of the file. A missing or empty file gets just the record. A file whose last byte is
    /// not a line feed first gets a line break, so the record never joins an unterminated final line.
    /// </summary>
    public static void Append(
        string path,
        LicenseRequest request,
        string keyId,
        byte[] licenseFile,
        DateTimeOffset recordedAt,
        LedgerFileLock ledgerLock,
        Guid? activationRequestId = null,
        Guid? customerId = null,
        string? issuedBy = null,
        string? renewedFromLicenseId = null)
    {
        ArgumentNullException.ThrowIfNull(ledgerLock);
        if (activationRequestId is not null && (customerId is null || issuedBy is null))
            throw new LicenseToolException("Request-bound ledger metadata must include request, customer, and issuer IDs.");
        if (activationRequestId is null && renewedFromLicenseId is null && (customerId is not null || issuedBy is not null))
            throw new LicenseToolException("Customer and actor metadata require an activation request or renewal predecessor.");
        if (renewedFromLicenseId is not null
            && (string.IsNullOrWhiteSpace(renewedFromLicenseId) || renewedFromLicenseId.Length > 64
                || renewedFromLicenseId.Any(char.IsControl) || renewedFromLicenseId == request.LicenseId
                || string.IsNullOrWhiteSpace(issuedBy)))
            throw new LicenseToolException("Renewal history must include a valid predecessor and authorized actor.");
        if (issuedBy is { Length: > 128 } || issuedBy?.Any(char.IsControl) == true)
            throw new LicenseToolException("The issuance actor is invalid.");
        var full = Path.GetFullPath(path);
        ledgerLock.EnsureFor(full);
        var directory = Path.GetDirectoryName(full)!;
        OutputPathGuard.EnsureOutsideGitWorkingTree(directory, "The ledger folder");
        Directory.CreateDirectory(directory);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("recordedAt", LicenseIssuer.FormatTimestamp(recordedAt));
            writer.WriteString("licenseId", request.LicenseId);
            writer.WriteString("licensee", request.Licensee);
            writer.WriteString("keyId", keyId);
            writer.WriteString("notBefore", LicenseIssuer.FormatTimestamp(request.NotBefore));
            if (request.ExpiresAt is { } expires) writer.WriteString("expiresAt", LicenseIssuer.FormatTimestamp(expires));
            else writer.WriteNull("expiresAt");
            writer.WriteNumber("graceDays", request.GraceDays);
            writer.WriteNumber("maxInstallations", request.MaxInstallations);
            if (request.InstallationId is not null) writer.WriteString("installationId", request.InstallationId);
            if (activationRequestId is { } requestId)
            {
                writer.WriteString("activationRequestId", requestId);
            }
            if (customerId is { } customer) writer.WriteString("customerId", customer);
            if (issuedBy is not null) writer.WriteString("issuedBy", issuedBy);
            if (renewedFromLicenseId is not null) writer.WriteString("renewedFromLicenseId", renewedFromLicenseId);
            writer.WriteString("fileSha256", Convert.ToHexString(SHA256.HashData(licenseFile)).ToLowerInvariant());
            writer.WriteEndObject();
        }

        // One write, under the caller's lock. If the file is not empty and its last byte is not a line feed (an
        // unterminated or incomplete final record), the new record starts on its own line. Existing bytes are never
        // edited, repaired or dropped, so evidence of an incomplete record is preserved.
        var newline = new UTF8Encoding(false).GetBytes(Environment.NewLine);
        var json = stream.ToArray();
        using var file = new FileStream(full, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        var needsSeparator = false;
        if (file.Length > 0)
        {
            file.Seek(-1, SeekOrigin.End);
            needsSeparator = file.ReadByte() != '\n';
        }
        file.Seek(0, SeekOrigin.End);
        var separatorLength = needsSeparator ? newline.Length : 0;
        var buffer = new byte[separatorLength + json.Length + newline.Length];
        if (needsSeparator) Buffer.BlockCopy(newline, 0, buffer, 0, newline.Length);
        Buffer.BlockCopy(json, 0, buffer, separatorLength, json.Length);
        Buffer.BlockCopy(newline, 0, buffer, separatorLength + json.Length, newline.Length);
        file.Write(buffer, 0, buffer.Length);
        file.Flush(true);
    }

    /// <summary>Reads one ledger row under the caller's lock; malformed rows or duplicate ids fail closed.</summary>
    public static IssuedLicenseRecord? FindById(string path, string licenseId, LedgerFileLock ledgerLock)
    {
        ArgumentNullException.ThrowIfNull(ledgerLock);
        var full = Path.GetFullPath(path);
        ledgerLock.EnsureFor(full);
        if (!File.Exists(full)) return null;
        const long maxBytes = 50L * 1024 * 1024;
        byte[] bytes;
        try
        {
            if (new FileInfo(full).Length > maxBytes)
                throw new LicenseToolException("The issue ledger is larger than 50 MB, so renewal is refused.", 2);
            bytes = File.ReadAllBytes(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new LicenseToolException("The issue ledger cannot be read, so renewal is refused.", 2); }

        var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset); }
        catch (DecoderFallbackException) { throw new LicenseToolException("The issue ledger is not valid UTF-8; renewal is refused.", 2); }

        IssuedLicenseRecord? found = null;
        using var reader = new StringReader(text);
        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !TryString(root, "licenseId", out var id) || id.Length is 0 or > 64
                    || !TryString(root, "licensee", out var licensee) || licensee.Length > 200
                    || !TryString(root, "fileSha256", out var hash) || hash.Length != 64
                    || hash.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')))
                    throw new FormatException();
                var installation = OptionalString(root, "installationId");
                if (installation is not null && !InstallationIds.IsValid(installation)) throw new FormatException();
                var customerId = OptionalGuid(root, "customerId");
                var requestId = OptionalGuid(root, "activationRequestId");
                var actor = OptionalString(root, "issuedBy");
                var renewedFrom = OptionalString(root, "renewedFromLicenseId");
                if ((requestId is not null && (customerId is null || actor is null || installation is null))
                    || (renewedFrom is not null && (renewedFrom.Length is 0 or > 64 || actor is null)))
                    throw new FormatException();
                if (string.Equals(id, licenseId, StringComparison.Ordinal))
                {
                    if (found is not null) throw new LicenseToolException("The issue ledger contains duplicate license ids; renewal is refused.", 2);
                    found = new IssuedLicenseRecord(id, licensee, hash, installation, customerId, requestId, actor, renewedFrom);
                }
            }
            catch (LicenseToolException) { throw; }
            catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
            {
                throw new LicenseToolException("Line " + lineNumber + " of the issue ledger is malformed; renewal is refused.", 2);
            }
        }
        return found;
    }

    /// <summary>Allows at most one direct renewal successor for a predecessor while the caller holds the ledger lock.</summary>
    public static void EnsureNotRenewed(string path, string licenseId, LedgerFileLock ledgerLock)
    {
        ArgumentNullException.ThrowIfNull(ledgerLock);
        var full = Path.GetFullPath(path);
        ledgerLock.EnsureFor(full);
        if (!File.Exists(full)) return;
        if (new FileInfo(full).Length > 50L * 1024 * 1024)
            throw new LicenseToolException("The issue ledger is larger than 50 MB; renewal is refused.", 2);
        string text;
        try
        {
            var bytes = File.ReadAllBytes(full);
            var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            text = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
        { throw new LicenseToolException("The issue ledger cannot be read safely; renewal is refused.", 2); }
        using var reader = new StringReader(text);
        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("licenseId", out var id) || id.ValueKind != JsonValueKind.String)
                    throw new FormatException();
                if (root.TryGetProperty("renewedFromLicenseId", out var predecessor))
                {
                    if (predecessor.ValueKind != JsonValueKind.String) throw new FormatException();
                    if (string.Equals(predecessor.GetString(), licenseId, StringComparison.Ordinal))
                        throw new LicenseToolException("This license already has a renewal successor.");
                }
            }
            catch (LicenseToolException) { throw; }
            catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
            { throw new LicenseToolException("Line " + lineNumber + " of the issue ledger is malformed; renewal is refused.", 2); }
        }
    }

    private static bool TryString(JsonElement root, string name, out string value)
    {
        value = "";
        if (!root.TryGetProperty(name, out var item) || item.ValueKind != JsonValueKind.String) return false;
        value = item.GetString() ?? "";
        return value.Length > 0;
    }

    private static string? OptionalString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var item)) return null;
        return item.ValueKind == JsonValueKind.String ? item.GetString() : throw new FormatException();
    }

    private static Guid? OptionalGuid(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var item)) return null;
        return item.ValueKind == JsonValueKind.String && Guid.TryParse(item.GetString(), out var value) && value != Guid.Empty
            ? value : throw new FormatException();
    }

    /// <summary>
    /// Refuses issuance when the license id is already recorded. The caller must hold the ledger lock and keep it
    /// until its append is finished. Fails closed: a ledger that is oversized, not valid UTF-8, unreadable, or has
    /// any line that is not a record with a license id means the duplicate check cannot be trusted, so nothing is issued.
    /// A missing ledger is empty.
    /// </summary>
    public static void EnsureNotIssued(string path, string licenseId, LedgerFileLock ledgerLock)
    {
        ArgumentNullException.ThrowIfNull(ledgerLock);
        var full = Path.GetFullPath(path);
        ledgerLock.EnsureFor(full);
        if (!File.Exists(full)) return;

        const long maxBytes = 50L * 1024 * 1024;
        byte[] bytes;
        try
        {
            if (new FileInfo(full).Length > maxBytes)
                throw new LicenseToolException("The issue ledger is larger than 50 MB, so nothing was issued.", 2);
            bytes = File.ReadAllBytes(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LicenseToolException(
                "The issue ledger cannot be read (" + ex.GetType().Name + "), so nothing was issued.", 2);
        }

        var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            throw new LicenseToolException("The issue ledger is not valid UTF-8, so nothing was issued.", 2);
        }

        using var reader = new StringReader(text);
        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            string? existingId = null;
            try
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("licenseId", out var id)
                    && id.ValueKind == JsonValueKind.String)
                    existingId = id.GetString();
            }
            catch (JsonException)
            {
                // handled below as an unreadable record
            }

            if (existingId is null)
                throw new LicenseToolException(
                    "Line " + lineNumber + " of the issue ledger is not a readable record, so the duplicate check "
                    + "cannot be trusted and nothing was issued. Repair or restore the ledger first.", 2);

            if (string.Equals(existingId, licenseId, StringComparison.Ordinal))
                throw new LicenseToolException("The license id '" + licenseId + "' was already issued. Use a new id.");
        }
    }
}
