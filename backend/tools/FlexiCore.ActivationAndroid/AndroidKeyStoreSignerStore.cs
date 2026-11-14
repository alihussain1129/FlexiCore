using System.Security.Cryptography;
using System.Text.Json;
using Android.Security.Keystore;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;
using Java.Security;
using Java.Security.Spec;
using ServiceKeyInfo = FlexiCore.ActivationEngine.Services.KeyInfo;
using LicenseKeyStore = FlexiCore.LicenseTool.KeyStore;

namespace FlexiCore.ActivationApp;

internal sealed class AndroidKeyStoreSignerStore(OwnerPaths paths) : IPlatformSigningKeyStore
{
    private const string Provider = "AndroidKeyStore";
    private const string AliasPrefix = "com.flexicore.activationengine.signing.";
    private const string MetadataSuffix = ".android-key.json";

    private sealed record KeyMetadata(string KeyId, DateTimeOffset CreatedUtc);

    public IReadOnlyList<ServiceKeyInfo> List()
    {
        Directory.CreateDirectory(paths.KeysDirectory);
        var result = new List<ServiceKeyInfo>();
        foreach (var metadataPath in Directory.GetFiles(paths.KeysDirectory, "*" + MetadataSuffix))
        {
            var fileId = Path.GetFileName(metadataPath)[..^MetadataSuffix.Length];
            try
            {
                var metadata = JsonSerializer.Deserialize<KeyMetadata>(File.ReadAllText(metadataPath));
                if (metadata is null || !string.Equals(metadata.KeyId, fileId, StringComparison.Ordinal))
                    throw new InvalidDataException("The Android key metadata does not match its file name.");
                LicenseKeyStore.ValidateKeyId(metadata.KeyId);

                var publicPath = Path.Combine(paths.KeysDirectory, metadata.KeyId + ".public.json");
                string? fingerprint = null;
                string? problem = null;
                try
                {
                    var publicKey = PublicKeyExport.Read(publicPath)
                        .SingleOrDefault(key => key.KeyId == metadata.KeyId);
                    if (publicKey is null) problem = "The exported public key is missing or does not match this key id.";
                    else fingerprint = LicenseKeyStore.Fingerprint(publicKey.SubjectPublicKeyInfo);
                }
                catch (Exception ex) when (ex is LicenseToolException or IOException or UnauthorizedAccessException)
                {
                    problem = "The exported public key is missing or invalid.";
                }

                if (!AliasExists(Alias(metadata.KeyId)))
                    problem = "The Android Keystore entry is unavailable. This device-bound key cannot be restored from a file backup.";

                result.Add(new ServiceKeyInfo(
                    metadata.KeyId,
                    "Android Keystore alias (non-exportable)",
                    File.Exists(publicPath) ? publicPath : null,
                    fingerprint,
                    metadata.CreatedUtc,
                    problem,
                    IsNonExportable: true));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or LicenseToolException)
            {
                result.Add(new ServiceKeyInfo(
                    fileId,
                    "Android Keystore alias (non-exportable)",
                    null,
                    null,
                    DateTimeOffset.MinValue,
                    "Android key metadata is invalid (" + ex.GetType().Name + ").",
                    IsNonExportable: true));
            }
        }
        return result;
    }

    public bool Contains(string keyId)
    {
        LicenseKeyStore.ValidateKeyId(keyId);
        return File.Exists(MetadataPath(keyId));
    }

    public ServiceKeyInfo Create(string keyId)
    {
        LicenseKeyStore.ValidateKeyId(keyId);
        var alias = Alias(keyId);
        var metadataPath = MetadataPath(keyId);
        var publicPath = Path.Combine(paths.KeysDirectory, keyId + ".public.json");
        if (File.Exists(metadataPath) || File.Exists(publicPath) || AliasExists(alias))
            throw new LicenseToolException($"A key named '{keyId}' already exists.");

        Directory.CreateDirectory(paths.KeysDirectory);
        using var generator = KeyPairGenerator.GetInstance(KeyProperties.KeyAlgorithmEc, Provider)
            ?? throw new LicenseToolException("Android did not provide an EC key-pair generator.");
        var parameterBuilder = new KeyGenParameterSpec.Builder(
            alias,
            KeyStorePurpose.Sign | KeyStorePurpose.Verify);
        parameterBuilder.SetAlgorithmParameterSpec(new ECGenParameterSpec("secp256r1"));
        parameterBuilder.SetDigests(KeyProperties.DigestSha256);
        parameterBuilder.SetUserAuthenticationRequired(false);
        var parameters = parameterBuilder.Build()
            ?? throw new LicenseToolException("Android could not build key-generation parameters.");
        generator.Initialize(parameters);
        try
        {
            using var pair = generator.GenerateKeyPair()
                ?? throw new LicenseToolException("Android did not create a key pair.");
            var privateKey = pair.Private
                ?? throw new LicenseToolException("Android did not create a private-key handle.");
            if (privateKey.GetEncoded() is not null)
                throw new LicenseToolException("Android returned an exportable private key. Key creation was refused.");
            var subjectPublicKeyInfo = pair.Public?.GetEncoded()
                ?? throw new LicenseToolException("Android did not return a public key.");
            _ = new LicenseVerifier(
                new[] { new TrustedKey(keyId, subjectPublicKeyInfo) },
                TimeProvider.System);

            var createdUtc = DateTimeOffset.UtcNow;
            var publicBytes = PublicKeyExport.Build(new[] { new TrustedKey(keyId, subjectPublicKeyInfo) });
            var metadataBytes = JsonSerializer.SerializeToUtf8Bytes(
                new KeyMetadata(keyId, createdUtc),
                new JsonSerializerOptions { WriteIndented = true });
            SafeFile.WriteNew(publicPath, publicBytes);
            SafeFile.WriteNew(metadataPath, metadataBytes);

            return ToKeyInfo(keyId, subjectPublicKeyInfo, publicPath, createdUtc);
        }
        catch (Exception creationError)
        {
            try
            {
                DeleteFileIfExists(publicPath);
                DeleteFileIfExists(metadataPath);
                DeleteAlias(alias);
            }
            catch (Exception cleanupError)
            {
                throw new LicenseToolException(
                    "Android key creation failed (" + creationError.GetType().Name
                    + ") and cleanup failed (" + cleanupError.GetType().Name
                    + "). The device keystore alias may remain; review the key list before retrying.", 2);
            }
            throw;
        }
    }

    public ILicenseSigner OpenSigner(string keyId)
    {
        LicenseKeyStore.ValidateKeyId(keyId);
        var metadata = JsonSerializer.Deserialize<KeyMetadata>(File.ReadAllText(MetadataPath(keyId)));
        if (metadata is null || metadata.KeyId != keyId)
            throw new LicenseToolException("The Android Keystore key metadata is invalid.");

        using var store = OpenStore();
        var key = store.GetKey(Alias(keyId), null) as IPrivateKey;
        var publicKey = store.GetCertificate(Alias(keyId))?.PublicKey;
        if (key is null)
            throw new LicenseToolException(
                "The Android Keystore key is unavailable. A file backup cannot restore this device-bound key.");
        if (publicKey?.GetEncoded() is not { } publicBytes)
        {
            key.Dispose();
            throw new LicenseToolException("The Android Keystore public key is unavailable.");
        }
        if (key.GetEncoded() is not null)
        {
            key.Dispose();
            throw new LicenseToolException("Android returned an exportable private key. Signing was refused.");
        }
        return new AndroidKeyStoreSigner(keyId, key, publicBytes);
    }

    private static ServiceKeyInfo ToKeyInfo(
        string keyId, byte[] subjectPublicKeyInfo, string publicPath, DateTimeOffset createdUtc) =>
        new ServiceKeyInfo(
            keyId,
            "Android Keystore alias (non-exportable)",
            publicPath,
            LicenseKeyStore.Fingerprint(subjectPublicKeyInfo),
            createdUtc,
            null,
            IsNonExportable: true);

    private static string Alias(string keyId) => AliasPrefix + keyId;
    private string MetadataPath(string keyId) => Path.Combine(paths.KeysDirectory, keyId + MetadataSuffix);

    private static bool AliasExists(string alias)
    {
        using var store = OpenStore();
        return store.ContainsAlias(alias);
    }

    private static Java.Security.KeyStore OpenStore()
    {
        var store = Java.Security.KeyStore.GetInstance(Provider)
            ?? throw new LicenseToolException("Android Keystore is unavailable.");
        store.Load(null, null);
        return store;
    }

    private static void DeleteAlias(string alias)
    {
        using var store = OpenStore();
        if (store.ContainsAlias(alias)) store.DeleteEntry(alias);
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}

internal sealed class AndroidKeyStoreSigner(
    string keyId,
    IPrivateKey privateKey,
    byte[] subjectPublicKeyInfo) : ILicenseSigner
{
    public string KeyId { get; } = keyId;
    public byte[] SubjectPublicKeyInfo { get; } = subjectPublicKeyInfo;

    public byte[] Sign(byte[] data)
    {
        using var signer = Java.Security.Signature.GetInstance("SHA256withECDSA")
            ?? throw new CryptographicException("Android did not provide an ECDSA signer.");
        signer.InitSign(privateKey);
        signer.Update(data);
        var derSignature = signer.Sign()
            ?? throw new CryptographicException("Android did not return an ECDSA signature.");
        return DerSignatureToRaw(derSignature);
    }

    public void Dispose() => privateKey.Dispose();

    private static byte[] DerSignatureToRaw(byte[] der)
    {
        var source = der.AsSpan();
        var offset = 0;
        if (ReadByte(source, ref offset) != 0x30)
            throw new CryptographicException("Android returned an invalid ECDSA signature.");
        var sequenceLength = ReadLength(source, ref offset);
        if (sequenceLength != source.Length - offset)
            throw new CryptographicException("Android returned an invalid ECDSA signature length.");

        var r = ReadInteger(source, ref offset);
        var s = ReadInteger(source, ref offset);
        if (offset != source.Length)
            throw new CryptographicException("Android returned trailing ECDSA signature data.");

        var raw = new byte[64];
        WriteInteger(r, raw.AsSpan(0, 32));
        WriteInteger(s, raw.AsSpan(32, 32));
        return raw;
    }

    private static ReadOnlySpan<byte> ReadInteger(ReadOnlySpan<byte> der, ref int offset)
    {
        if (ReadByte(der, ref offset) != 0x02)
            throw new CryptographicException("Android returned an invalid ECDSA integer.");
        var length = ReadLength(der, ref offset);
        if (length == 0 || length > der.Length - offset)
            throw new CryptographicException("Android returned an invalid ECDSA integer length.");

        var value = der.Slice(offset, length);
        offset += length;
        if ((value[0] & 0x80) != 0)
            throw new CryptographicException("Android returned a negative ECDSA integer.");
        while (value.Length > 1 && value[0] == 0) value = value[1..];
        if (value.Length > 32)
            throw new CryptographicException("Android returned an oversized ECDSA integer.");
        return value;
    }

    private static void WriteInteger(ReadOnlySpan<byte> value, Span<byte> destination)
    {
        destination.Clear();
        value.CopyTo(destination[(destination.Length - value.Length)..]);
    }

    private static int ReadByte(ReadOnlySpan<byte> source, ref int offset)
    {
        if ((uint)offset >= (uint)source.Length)
            throw new CryptographicException("Android returned a truncated ECDSA signature.");
        return source[offset++];
    }

    private static int ReadLength(ReadOnlySpan<byte> source, ref int offset)
    {
        var first = ReadByte(source, ref offset);
        if (first < 0x80) return first;
        var count = first & 0x7f;
        if (count is 0 or > 2)
            throw new CryptographicException("Android returned an unsupported ECDSA signature length.");
        var length = 0;
        for (var index = 0; index < count; index++)
            length = checked((length << 8) | ReadByte(source, ref offset));
        return length;
    }
}
