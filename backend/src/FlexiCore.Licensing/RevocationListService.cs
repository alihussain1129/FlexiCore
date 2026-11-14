using System.Security.Cryptography;

namespace FlexiCore.Licensing;

public interface IRevocationListStore
{
    Task<byte[]?> ReadAsync(CancellationToken cancellationToken);
    Task WriteAsync(byte[] bytes, CancellationToken cancellationToken);
}

public sealed class FileRevocationListStore(string directory) : IRevocationListStore
{
    private readonly string _directory = Path.IsPathFullyQualified(directory)
        ? directory : throw new ArgumentException("The revocation directory must be absolute.", nameof(directory));
    private string PathName => Path.Combine(_directory, "revocations.signed.json");

    public async Task<byte[]?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(PathName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            var buffer = new byte[SignedRevocationListVerifier.MaxFileBytes + 1];
            var total = 0;
            while (total < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken);
                if (read == 0) break;
                total += read;
            }
            if (total > SignedRevocationListVerifier.MaxFileBytes)
                throw new LicenseStorageException("The revocation snapshot is too large.");
            return buffer.AsSpan(0, total).ToArray();
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (LicenseStorageException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new LicenseStorageException("The revocation snapshot could not be read.", ex); }
    }

    public async Task WriteAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length is 0 or > SignedRevocationListVerifier.MaxFileBytes)
            throw new LicenseStorageException("The revocation snapshot has an invalid size.");
        var temp = Path.Combine(_directory, ".revocations." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            Directory.CreateDirectory(_directory);
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
                BufferSize = 4096, Options = FileOptions.Asynchronous,
            };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var stream = new FileStream(temp, options))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(true);
            }
            File.Move(temp, PathName, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(temp); } catch (IOException) { }
            throw new LicenseStorageException("The revocation snapshot could not be stored.", ex);
        }
        catch
        {
            try { File.Delete(temp); } catch (IOException) { }
            throw;
        }
    }
}

public enum RevocationCheckState { Clear, Revoked, Unavailable }
public sealed record RevocationCheck(RevocationCheckState State, string? Reason, DateTimeOffset? NextUpdate);
public enum RevocationImportOutcome { Imported, Unchanged, Rejected, OlderSnapshot, RollbackAttempt, VerificationUnavailable, StorageFailed }
public sealed record RevocationImportResult(RevocationImportOutcome Outcome, RevocationListError Error = RevocationListError.None);

/// <summary>
/// Accepts only trusted signed snapshots. Revocations are monotonic: a later snapshot cannot remove or alter an
/// earlier revocation. A stale installed snapshot fails closed; without any snapshot yet, status is unknown/clear
/// until synchronization is configured by the deployment.
/// </summary>
public sealed class RevocationListService
{
    private readonly IRevocationListStore _store;
    private readonly SignedRevocationListVerifier _verifier;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public RevocationListService(IRevocationListStore store, SignedRevocationListVerifier verifier, TimeProvider time)
    { _store = store; _verifier = verifier; _time = time; }

    public async Task<RevocationCheck> CheckAsync(string licenseId, CancellationToken cancellationToken)
    {
        byte[]? bytes;
        try { bytes = await _store.ReadAsync(cancellationToken); }
        catch (LicenseStorageException) { return new(RevocationCheckState.Unavailable, "revocation_storage_error", null); }
        if (bytes is null) return new(RevocationCheckState.Clear, null, null);
        var result = _verifier.Verify(bytes);
        if (!result.IsValid || result.List is null)
            return new(RevocationCheckState.Unavailable, "revocation_snapshot_invalid", null);
        if (result.List.NextUpdate <= _time.GetUtcNow())
            return new(RevocationCheckState.Unavailable, "revocation_snapshot_stale", result.List.NextUpdate);
        var entry = result.List.RevokedLicenses.FirstOrDefault(x => string.Equals(x.LicenseId, licenseId, StringComparison.Ordinal));
        return entry is null
            ? new(RevocationCheckState.Clear, null, result.List.NextUpdate)
            : new(RevocationCheckState.Revoked, "license_revoked", result.List.NextUpdate);
    }

    public async Task<RevocationImportResult> ImportAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        var candidate = _verifier.Verify(bytes);
        if (!candidate.IsValid || candidate.List is null)
            return new(candidate.Error == RevocationListError.NoTrustedKeys
                ? RevocationImportOutcome.VerificationUnavailable : RevocationImportOutcome.Rejected, candidate.Error);
        var now = _time.GetUtcNow();
        if (candidate.List.IssuedAt > now.AddMinutes(5) || candidate.List.NextUpdate <= now)
            return new(RevocationImportOutcome.Rejected, RevocationListError.InvalidPayload);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? currentBytes;
            try { currentBytes = await _store.ReadAsync(cancellationToken); }
            catch (LicenseStorageException) { return new(RevocationImportOutcome.StorageFailed); }
            if (currentBytes is not null)
            {
                var current = _verifier.Verify(currentBytes);
                if (!current.IsValid || current.List is null)
                    return new(RevocationImportOutcome.StorageFailed, current.Error);
                if (currentBytes.AsSpan().SequenceEqual(bytes)) return new(RevocationImportOutcome.Unchanged);
                if (candidate.List.IssuedAt <= current.List.IssuedAt)
                    return new(RevocationImportOutcome.OlderSnapshot);
                var byId = candidate.List.RevokedLicenses.ToDictionary(x => x.LicenseId, StringComparer.Ordinal);
                foreach (var old in current.List.RevokedLicenses)
                    if (!byId.TryGetValue(old.LicenseId, out var updated) || updated != old)
                        return new(RevocationImportOutcome.RollbackAttempt);
            }
            try { await _store.WriteAsync(bytes, cancellationToken); }
            catch (LicenseStorageException) { return new(RevocationImportOutcome.StorageFailed); }
            return new(RevocationImportOutcome.Imported);
        }
        finally { _gate.Release(); }
    }
}
