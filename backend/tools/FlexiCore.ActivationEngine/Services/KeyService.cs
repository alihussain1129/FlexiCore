using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Services;

public sealed record KeyInfo(
    string KeyId,
    string PrivateKeyPath,
    string? PublicKeyPath,
    string? Fingerprint,
    DateTimeOffset CreatedUtc,
    string? Problem);

/// <summary>Thin layer over the existing KeyStore and PublicKeyExport. Private key bytes never pass through here.</summary>
public sealed class KeyService
{
    private readonly OwnerPaths _paths;

    public KeyService(OwnerPaths paths) => _paths = paths;

    public IReadOnlyList<KeyInfo> List(out string? problem)
    {
        problem = null;
        var result = new List<KeyInfo>();
        if (!Directory.Exists(_paths.KeysDirectory)) return result;

        string[] files;
        try
        {
            files = Directory.GetFiles(_paths.KeysDirectory, "*.p8");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problem = "The key folder cannot be read (" + ex.GetType().Name + ").";
            return result;
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
