namespace FlexiCore.Licensing;

/// <summary>Persists the single imported license file. Implementations keep the exact bytes and never interpret them.</summary>
public interface ILicenseStore
{
    /// <summary>
    /// Returns the stored bytes, or null when nothing is stored. Never returns more than
    /// <see cref="LicenseLimits.MaxFileBytes"/> + 1 bytes, so an oversized file is detectable without loading it all.
    /// </summary>
    Task<byte[]?> ReadAsync(CancellationToken cancellationToken);

    /// <summary>Replaces the stored license atomically. If this throws, the previous license is unchanged.</summary>
    Task WriteAsync(byte[] content, CancellationToken cancellationToken);
}

/// <summary>The store could not read or write the license file (I/O or permission problem).</summary>
public sealed class LicenseStorageException(string message, Exception? inner = null) : Exception(message, inner);