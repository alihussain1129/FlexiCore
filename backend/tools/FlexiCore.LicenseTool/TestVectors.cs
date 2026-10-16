using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlexiCore.Licensing;

namespace FlexiCore.LicenseTool;

/// <summary>
/// Generates the public interoperability vectors: a test public key set plus four license files. The signing key exists
/// only in memory for the duration of the call and is never written anywhere. Signatures are randomized, so the files
/// are generated once and pinned by hash; regenerating them is a deliberate act.
/// </summary>
public static class TestVectors
{
    public const string KeyId = "interop-test-1";
    public const string PublicKeysFile = "interop-test-public-keys.json";

    public static readonly string[] FileNames =
        [PublicKeysFile, "valid.lic", "expired.lic", "wrong-product.lic", "tampered.lic"];

    /// <summary>The clock the vectors are meant to be verified at.</summary>
    private static readonly DateTimeOffset Clock = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    public static int Run(string[] rest, IToolConsole console)
    {
        var p = ArgParser.Parse(rest, ["out"], ["force"]);
        var directory = Path.GetFullPath(p.Require("out"));
        var force = p.Flags.Contains("force");
        foreach (var name in FileNames)
        {
            if (!force && File.Exists(Path.Combine(directory, name)))
                throw new LicenseToolException(
                    $"{name} already exists in {directory}. The vectors are pinned by hash; regenerate only on purpose, with --force.");
        }

        using var signer = new EcdsaSigner(ECDsa.Create(ECCurve.NamedCurves.nistP256), KeyId);

        var valid = LicenseIssuer.Issue(Request("LIC-VECTOR-VALID", "2027-01-01T00:00:00Z"), signer);
        var expired = LicenseIssuer.Issue(Request("LIC-VECTOR-EXPIRED", "2026-05-01T00:00:00Z"), signer);
        var wrongProduct = LicenseIssuer.IssueUnchecked(
            Request("LIC-VECTOR-WRONGPRODUCT", "2027-01-01T00:00:00Z", "OtherProduct"), signer);
        var tampered = Tamper(valid);
        var keySet = PublicKeyExport.Build(new[] { new TrustedKey(KeyId, signer.SubjectPublicKeyInfo) });

        // Nothing is written unless the real verifier gives the expected answer for every vector.
        var verifier = new LicenseVerifier(
            new[] { new TrustedKey(KeyId, signer.SubjectPublicKeyInfo) }, new PinnedTimeProvider(Clock));
        Expect(verifier, "valid", valid, LicenseStatus.Valid);
        Expect(verifier, "expired", expired, LicenseStatus.Expired);
        Expect(verifier, "wrong-product", wrongProduct, LicenseStatus.WrongProduct);
        Expect(verifier, "tampered", tampered, LicenseStatus.InvalidSignature);

        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, PublicKeysFile), keySet);
        File.WriteAllBytes(Path.Combine(directory, "valid.lic"), valid);
        File.WriteAllBytes(Path.Combine(directory, "expired.lic"), expired);
        File.WriteAllBytes(Path.Combine(directory, "wrong-product.lic"), wrongProduct);
        File.WriteAllBytes(Path.Combine(directory, "tampered.lic"), tampered);

        console.Out.WriteLine($"Wrote {FileNames.Length} vector files to {directory}");
        console.Out.WriteLine($"Test key id    : {KeyId}");
        console.Out.WriteLine($"Fingerprint    : {KeyStore.Fingerprint(signer.SubjectPublicKeyInfo)}");
        console.Out.WriteLine("Verified at 2026-06-01T00:00:00Z: valid=Valid, expired=Expired, wrong-product=WrongProduct, tampered=InvalidSignature");
        console.Out.WriteLine("The signing key existed only in memory and has been discarded. These are TEST vectors:");
        console.Out.WriteLine("the id 'interop-test-1' must never appear in a production key set.");
        return 0;
    }

    private static LicenseRequest Request(string id, string expires, string product = LicenseLimits.ExpectedProduct) =>
        new(id, "Interop Test Customer", Time("2026-01-01T00:00:00Z"), Time("2026-01-01T00:00:00Z"),
            Time(expires), 0, 1, product);

    private static DateTimeOffset Time(string text) =>
        LicenseIssuer.TryParseTimestamp(text, out var value) ? value : throw new InvalidOperationException(text);

    private static void Expect(LicenseVerifier verifier, string name, byte[] file, LicenseStatus expected)
    {
        var actual = verifier.Verify(file).Status;
        if (actual != expected)
            throw new LicenseToolException($"Vector '{name}' verified as {actual}, expected {expected}. Nothing was written.", 2);
    }

    /// <summary>Keeps the valid license's signature but changes a signed field, so the signature no longer matches.</summary>
    private static byte[] Tamper(byte[] file)
    {
        using var doc = JsonDocument.Parse(file);
        var root = doc.RootElement;
        var keyId = root.GetProperty("keyId").GetString()!;
        var signature = root.GetProperty("signature").GetString()!;
        var original = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(root.GetProperty("payload").GetString()!));
        var changed = original.Replace("\"graceDays\":0", "\"graceDays\":300", StringComparison.Ordinal);
        if (changed == original) throw new LicenseToolException("Could not build the tampered vector.", 2);

        return Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":1,\"keyId\":\"" + keyId + "\",\"payload\":\""
            + Base64Url.EncodeToString(Encoding.UTF8.GetBytes(changed))
            + "\",\"signature\":\"" + signature + "\"}");
    }
}