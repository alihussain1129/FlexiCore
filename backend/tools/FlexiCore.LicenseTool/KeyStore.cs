using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FlexiCore.Licensing;

namespace FlexiCore.LicenseTool;

/// <summary>Signs data with a private key. Kept behind an interface so a KMS or hardware token can replace the file key later.</summary>
public interface ILicenseSigner : IDisposable
{
    string KeyId { get; }
    byte[] SubjectPublicKeyInfo { get; }
    /// <summary>ES256: SHA-256 over the data, signature as raw 64-byte r||s.</summary>
    byte[] Sign(byte[] data);
}

public sealed class EcdsaSigner : ILicenseSigner
{
    private readonly ECDsa _key;

    public EcdsaSigner(ECDsa key, string keyId)
    {
        ArgumentNullException.ThrowIfNull(key);
        KeyStore.ValidateKeyId(keyId);
        KeyId = keyId;
        SubjectPublicKeyInfo = key.ExportSubjectPublicKeyInfo();

        try
        {
            _ = new LicenseVerifier(new[] { new TrustedKey(keyId, SubjectPublicKeyInfo) }, TimeProvider.System);
        }
        catch (ArgumentException)
        {
            throw new LicenseToolException("The key is not an ECDSA P-256 key.", 2);
        }

        _key = key;
    }

    public string KeyId { get; }
    public byte[] SubjectPublicKeyInfo { get; }

    public byte[] Sign(byte[] data) =>
        _key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    public void Dispose() => _key.Dispose();
}

/// <summary>Creates and loads passphrase-encrypted private key files (PKCS#8, PBES2, AES-256, PBKDF2-SHA256).</summary>
public static class KeyStore
{
    public const int Pbkdf2Iterations = 600_000;
    private const string EncryptedLabel = "ENCRYPTED PRIVATE KEY";
    private static readonly Regex IdPattern = new(@"^[A-Za-z0-9._-]{1,64}\z", RegexOptions.CultureInvariant);

    public sealed record CreatedKey(string PrivateKeyPath, string PublicKeyPath, string Fingerprint, bool AccessRestricted);

    public static void ValidateKeyId(string? keyId)
    {
        if (keyId is null || !IdPattern.IsMatch(keyId))
            throw new LicenseToolException("The key id must be 1 to 64 characters from A-Z, a-z, 0-9, '.', '_' and '-'.");
    }

    public static string Fingerprint(byte[] subjectPublicKeyInfo) =>
        Convert.ToHexString(SHA256.HashData(subjectPublicKeyInfo)).ToLowerInvariant();

    public static CreatedKey Create(string keyId, string directory, string passphrase, bool force) =>
        CreateCore(keyId, directory, passphrase, force, RestrictToCurrentUser);

    // Internal test seam: production callers cannot choose or bypass the ACL or move implementation.
    internal static CreatedKey CreateForTesting(
        string keyId,
        string directory,
        string passphrase,
        bool force,
        Func<string, bool> restrictPermissions,
        Action<string, string, bool>? move = null) =>
        CreateCore(keyId, directory, passphrase, force, restrictPermissions, move);

    private static CreatedKey CreateCore(
        string keyId,
        string directory,
        string passphrase,
        bool force,
        Func<string, bool> restrictPermissions,
        Action<string, string, bool>? move = null)
    {
        ValidateKeyId(keyId);
        OutputPathGuard.EnsureOutsideGitWorkingTree(directory, "The key directory");

        move ??= File.Move;

        var privatePath = Path.Combine(directory, keyId + ".p8");
        var publicPath = Path.Combine(directory, keyId + ".public.json");

        if (!force && (File.Exists(privatePath) || File.Exists(publicPath)))
            throw new LicenseToolException($"A key named '{keyId}' already exists in that folder. Use --force only if you are sure.");

        Directory.CreateDirectory(directory);

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var spki = key.ExportSubjectPublicKeyInfo();
        var publicJson = PublicKeyExport.Build(new[] { new TrustedKey(keyId, spki) });
        var encrypted = key.ExportEncryptedPkcs8PrivateKey(
            passphrase.AsSpan(),
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, Pbkdf2Iterations));
        var pem = Encoding.ASCII.GetBytes(new string(PemEncoding.Write(EncryptedLabel, encrypted)) + "\n");

        var token = Guid.NewGuid().ToString("N");
        var tempPrivate = Path.Combine(directory, $".{keyId}.{token}.p8.tmp");
        var tempPublic = Path.Combine(directory, $".{keyId}.{token}.public.json.tmp");
        var backupPrivate = Path.Combine(directory, $".{keyId}.{token}.p8.bak");
        var backupPublic = Path.Combine(directory, $".{keyId}.{token}.public.json.bak");

        bool hadPrivate = false, hadPublic = false, newPrivateInPlace = false, newPublicInPlace = false;

        try
        {
            // Stage both files first. The private file is ACL-restricted before any key bytes are written.
            WritePrivateFile(tempPrivate, pem, restrictPermissions);
            File.WriteAllBytes(tempPublic, publicJson);

            // Commit: set the old pair aside, then put the new pair in place.
            if (File.Exists(privatePath)) { move(privatePath, backupPrivate, false); hadPrivate = true; }
            if (File.Exists(publicPath)) { move(publicPath, backupPublic, false); hadPublic = true; }

            move(tempPrivate, privatePath, false);
            newPrivateInPlace = true;
            move(tempPublic, publicPath, false);
            newPublicInPlace = true;
        }
        catch
        {
            var rollbackProblems = new List<string>();
            Restore(privatePath, backupPrivate, hadPrivate, newPrivateInPlace, rollbackProblems);
            Restore(publicPath, backupPublic, hadPublic, newPublicInPlace, rollbackProblems);
            TryDelete(tempPrivate);
            TryDelete(tempPublic);

            if (rollbackProblems.Count > 0)
                throw new LicenseToolException(
                    "Key generation failed and the previous key pair could not be fully restored. "
                    + "Check these files before doing anything else: " + string.Join("; ", rollbackProblems));

            throw;
        }

        // Success: the old pair is no longer needed.
        TryDelete(backupPrivate);
        TryDelete(backupPublic);

        return new CreatedKey(privatePath, publicPath, Fingerprint(spki), true);
    }

    private static void Restore(string target, string backup, bool hadOriginal, bool newInPlace, List<string> problems)
    {
        try
        {
            if (hadOriginal)
            {
                if (File.Exists(target)) File.Delete(target);
                File.Move(backup, target, false);
            }
            else if (newInPlace && File.Exists(target))
            {
                File.Delete(target);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problems.Add(hadOriginal ? $"'{target}' (original saved at '{backup}')" : $"'{target}'");
        }
    }
    /// <summary>The key id is the file name without its extension (fc-2026-a.p8 has the id fc-2026-a).</summary>
    public static EcdsaSigner Load(string path, string passphrase)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new LicenseToolException($"Key file not found: {full}");
        var keyId = Path.GetFileNameWithoutExtension(full);
        ValidateKeyId(keyId);

        var text = File.ReadAllText(full);
        if (!PemEncoding.TryFind(text, out var fields)
            || !text[fields.Label].Equals(EncryptedLabel, StringComparison.Ordinal))
            throw new LicenseToolException("The key file is not a passphrase-encrypted key. Unencrypted keys are refused.");

        byte[] der;
        try { der = Convert.FromBase64String(text[fields.Base64Data]); }
        catch (FormatException) { throw new LicenseToolException("The key file is damaged."); }

        var key = ECDsa.Create();
        try
        {
            key.ImportEncryptedPkcs8PrivateKey(passphrase.AsSpan(), der, out _);
            return new EcdsaSigner(key, keyId);
        }
        catch (CryptographicException)
        {
            key.Dispose();
            throw new LicenseToolException("The passphrase is wrong or the key file is damaged.", 2);
        }
        catch (LicenseToolException)
        {
            key.Dispose();
            throw;
        }
    }

    private static void WritePrivateFile(string path, byte[] data, Func<string, bool> restrictPermissions)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };

        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        // Create an empty file first. No private-key bytes are written until ACL restriction succeeds.
        using (new FileStream(path, options)) { }

        if (!restrictPermissions(path))
        {
            var removed = TryDelete(path);
            var detail = removed
                ? "The empty temporary file was removed."
                : $"The temporary file remains at '{path}' and must be deleted manually; it contains no key material.";

            throw new LicenseToolException(
                $"Could not restrict the private-key file to the current user. Key generation was stopped. {detail}");
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
            stream.Write(data);
            stream.Flush(true);
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return !File.Exists(path);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    /// <summary>Removes inherited Windows permissions and grants full control only to the current user.</summary>
    private static bool RestrictToCurrentUser(string path)
    {
        if (!OperatingSystem.IsWindows()) return true;

        try
        {
            var start = new ProcessStartInfo("icacls")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add(path);
            start.ArgumentList.Add("/inheritance:r");
            start.ArgumentList.Add("/grant:r");
            start.ArgumentList.Add($"{Environment.UserDomainName}\\{Environment.UserName}:F");

            using var process = Process.Start(start);
            if (process is null) return false;

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
