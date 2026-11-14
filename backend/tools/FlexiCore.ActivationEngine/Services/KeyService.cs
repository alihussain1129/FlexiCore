using System.Security.Cryptography;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Services;

public sealed record KeyInfo(
    string KeyId,
    string PrivateKeyPath,
    string? PublicKeyPath,
    string? Fingerprint,
    DateTimeOffset CreatedUtc,
    string? Problem,
    bool IsNonExportable = false);

public interface IPlatformSigningKeyStore
{
    IReadOnlyList<KeyInfo> List();
    bool Contains(string keyId);
    KeyInfo Create(string keyId);
    FlexiCore.LicenseTool.ILicenseSigner OpenSigner(string keyId);
}

/// <summary>Thin layer over the existing KeyStore and PublicKeyExport. Private key bytes never pass through here.</summary>
public sealed class KeyService
{
    private readonly OwnerPaths _paths;
    private readonly IPlatformSigningKeyStore? _platformKeys;

    public KeyService(OwnerPaths paths, IPlatformSigningKeyStore? platformKeys = null)
    {
        _paths = paths;
        _platformKeys = platformKeys;
    }

    public bool SupportsPlatformKeys => _platformKeys is not null;

    public bool IsPlatformKey(string keyId) => _platformKeys?.Contains(keyId) == true;

    public FlexiCore.LicenseTool.ILicenseSigner OpenSigner(string keyId, string passphrase) =>
        IsPlatformKey(keyId)
            ? _platformKeys!.OpenSigner(keyId)
            : KeyStore.Load(Path.Combine(_paths.KeysDirectory, keyId + ".p8"), passphrase);

    public KeyInfo CreatePlatformKey(string keyId)
    {
        if (_platformKeys is null)
            throw new LicenseToolException("This platform does not provide non-exportable signing keys.");

        KeyStore.ValidateKeyId(keyId);
        if (keyId.StartsWith("interop-test", StringComparison.Ordinal))
            throw new LicenseToolException("Key ids starting with 'interop-test' are reserved for test vectors.");
        if (List(out _).Any(k => string.Equals(k.KeyId, keyId, StringComparison.Ordinal)))
            throw new LicenseToolException($"A key named '{keyId}' already exists.");
        return _platformKeys.Create(keyId);
    }

    public IReadOnlyList<KeyInfo> List(out string? problem)
    {
        problem = null;
        var result = new List<KeyInfo>();
        var files = Array.Empty<string>();
        if (Directory.Exists(_paths.KeysDirectory))
        {
            try
            {
                files = Directory.GetFiles(_paths.KeysDirectory, "*.p8");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                problem = "The key folder cannot be read (" + ex.GetType().Name + ").";
            }
        }

        foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            try { KeyStore.ValidateKeyId(id); }
            catch (LicenseToolException) { continue; }

            var publicPath = Path.Combine(_paths.KeysDirectory, id + ".public.json");
            string? fingerprint = null;
            string? keyProblem = null;
            try
            {
                var keys = PublicKeyExport.Read(publicPath);
                var match = keys.FirstOrDefault(k => k.KeyId == id);
                if (match is null) keyProblem = "The public key file does not contain this key id.";
                else fingerprint = KeyStore.Fingerprint(match.SubjectPublicKeyInfo);
            }
            catch (Exception ex) when (ex is LicenseToolException or IOException or UnauthorizedAccessException)
            {
                keyProblem = "The public key file is missing or invalid.";
            }

            result.Add(new KeyInfo(
                id, file, File.Exists(publicPath) ? publicPath : null, fingerprint,
                File.GetCreationTimeUtc(file), keyProblem));
        }

        if (_platformKeys is not null)
        {
            try { result.AddRange(_platformKeys.List()); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
            {
                problem ??= "The platform key store cannot be read (" + ex.GetType().Name + ").";
            }
        }
        return result;
    }

    /// <summary>Creates an encrypted key pair. Never forces: an existing key is never replaced from this app.</summary>
    public KeyStore.CreatedKey Create(string keyId, string passphrase, string confirmation)
    {
        KeyStore.ValidateKeyId(keyId);
        if (keyId.StartsWith("interop-test", StringComparison.Ordinal))
            throw new LicenseToolException("Key ids starting with 'interop-test' are reserved for test vectors.");

        var problem = PassphrasePolicy.Validate(passphrase);
        if (problem is not null) throw new LicenseToolException(problem);
        if (!string.Equals(passphrase, confirmation, StringComparison.Ordinal))
            throw new LicenseToolException("The passphrases did not match. Nothing was written.");

        return KeyStore.Create(keyId, _paths.KeysDirectory, passphrase, force: false);
    }

    public KeyStore.CreatedKey ImportEncryptedKey(string sourcePath, string passphrase)
    {
        var fullSource = Path.GetFullPath(sourcePath);
        if (!string.Equals(Path.GetExtension(fullSource), ".p8", StringComparison.OrdinalIgnoreCase))
            throw new LicenseToolException("Choose an encrypted .p8 signing-key file.");
        var sourceInfo = new FileInfo(fullSource);
        if (!sourceInfo.Exists) throw new LicenseToolException("The selected signing-key file was not found.");
        if (sourceInfo.Length is <= 0 or > 1024 * 1024)
            throw new LicenseToolException("The encrypted signing-key file is empty or larger than 1 MB.");

        var keyId = Path.GetFileNameWithoutExtension(fullSource);
        KeyStore.ValidateKeyId(keyId);
        if (List(out _).Any(k => string.Equals(k.KeyId, keyId, StringComparison.Ordinal)))
            throw new LicenseToolException($"A key named '{keyId}' already exists. Nothing was imported.");

        byte[] privateBytes;
        byte[] publicBytes;
        string fingerprint;
        using (var signer = KeyStore.Load(fullSource, passphrase))
        {
            privateBytes = File.ReadAllBytes(fullSource);
            fingerprint = KeyStore.Fingerprint(signer.SubjectPublicKeyInfo);
            publicBytes = PublicKeyExport.Build(new[]
            {
                new TrustedKey(keyId, signer.SubjectPublicKeyInfo)
            });
        }

        Directory.CreateDirectory(_paths.KeysDirectory);
        var privateTarget = Path.Combine(_paths.KeysDirectory, keyId + ".p8");
        var publicTarget = Path.Combine(_paths.KeysDirectory, keyId + ".public.json");
        if (File.Exists(privateTarget) || File.Exists(publicTarget))
            throw new LicenseToolException($"A key named '{keyId}' already exists. Nothing was imported.");

        SafeFile.WriteNew(privateTarget, privateBytes);
        try
        {
            SafeFile.WriteNew(publicTarget, publicBytes);
        }
        catch
        {
            try { File.Delete(privateTarget); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new LicenseToolException(
                    "The public key could not be imported and the encrypted private-key file remains at '"
                    + privateTarget + "'. Check the pair before continuing.", 2);
            }
            throw;
        }

        return new KeyStore.CreatedKey(privateTarget, publicTarget, fingerprint, true);
    }

    public string ExportEncryptedPrivateKey(string keyId, string destination)
    {
        KeyStore.ValidateKeyId(keyId);
        if (IsPlatformKey(keyId))
            throw new LicenseToolException(
                "This signing key is non-exportable and cannot be backed up as a private-key file.");
        var source = Path.Combine(_paths.KeysDirectory, keyId + ".p8");
        if (!File.Exists(source)) throw new LicenseToolException("The encrypted signing-key file was not found.");
        SafeFile.CopyNew(source, destination);
        return destination;
    }

    /// <summary>Writes a public key set. The production key loader validates it before anything is written.</summary>
    public IReadOnlyList<(string KeyId, string Fingerprint)> ExportKeySet(IReadOnlyList<string> keyIds, string destination)
    {
        if (keyIds.Count == 0) throw new LicenseToolException("There is no key to export.");

        var all = new List<TrustedKey>();
        foreach (var id in keyIds)
        {
            KeyStore.ValidateKeyId(id);
            if (id.StartsWith("interop-test", StringComparison.Ordinal))
                throw new LicenseToolException("'" + id + "' is a test key id and must never be in a key set for customers.");

            var keys = PublicKeyExport.Read(Path.Combine(_paths.KeysDirectory, id + ".public.json"));
            var mine = keys.Where(k => k.KeyId == id).ToList();
            if (mine.Count != 1)
                throw new LicenseToolException("The public key file does not contain exactly the key '" + id + "'.");
            all.Add(mine[0]);
        }

        var bytes = PublicKeyExport.Build(all);
        SafeFile.WriteNew(destination, bytes);
        return all.Select(k => (k.KeyId, KeyStore.Fingerprint(k.SubjectPublicKeyInfo))).ToList();
    }
}
