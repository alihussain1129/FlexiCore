namespace FlexiCore.Licensing;

/// <summary>
/// Stores the license as one file with a fixed name inside an absolute directory. Writes go to a temporary file in the
/// same directory and are moved over the old file, so a failed write leaves the previous license untouched.
/// The file is signed, not secret: anyone who can write to the directory can delete or replace it.
/// </summary>
public sealed class FileLicenseStore : ILicenseStore
{
    public const string FileName = "license.lic";

    private readonly string _directory;

    public FileLicenseStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            throw new ArgumentException("The license directory must be an absolute path.", nameof(directory));
        _directory = directory;
    }

    private string FilePath => Path.Combine(_directory, FileName);

    public async Task<byte[]?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            var buffer = new byte[LicenseLimits.MaxFileBytes + 1];
            var total = 0;
            while (total < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken);
                if (read == 0) break;
                total += read;
            }
            return buffer.AsSpan(0, total).ToArray();
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LicenseStorageException("The stored license file could not be read.", ex);
        }
    }

    public async Task WriteAsync(byte[] content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var temp = Path.Combine(_directory, FileName + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            Directory.CreateDirectory(_directory);
            await using (var stream = new FileStream(
                temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(true); // push to disk before the move
            }
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            throw new LicenseStorageException("The license file could not be written.", ex);
        }
        catch
        {
            TryDelete(temp); // cancellation or anything unexpected: do not leave temp files behind
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}