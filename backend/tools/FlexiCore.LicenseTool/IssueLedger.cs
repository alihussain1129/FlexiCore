using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FlexiCore.LicenseTool;

/// <summary>
/// Append-only record of what was issued (one JSON line each). There is no revocation, so this is the owner's only
/// answer to "what did I sign?". It holds customer names, so it must live outside every git working tree.
/// </summary>
public static class IssueLedger
{
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
        LedgerFileLock ledgerLock)
    {
        ArgumentNullException.ThrowIfNull(ledgerLock);
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