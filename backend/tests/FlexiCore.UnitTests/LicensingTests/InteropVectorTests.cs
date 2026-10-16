using System.Security.Cryptography;
using FlexiCore.Licensing;

namespace FlexiCore.UnitTests.LicensingTests;

/// <summary>
/// Verifies the committed vectors (public key plus signed files, no private key) with the real verifier. These files were
/// produced by the owner tool, not by the test signer, so this checks the tool and the verifier against each other.
/// </summary>
public sealed class InteropVectorTests
{
    private static readonly DateTimeOffset Clock = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private const string PublicKeysFile = "interop-test-public-keys.json";

    private static string VectorDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FlexiCore.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "tests", "FlexiCore.UnitTests", "LicensingTests", "Vectors");
    }

    private static byte[] Read(string name) => File.ReadAllBytes(Path.Combine(VectorDir(), name));

    private static LicenseVerifier Verifier(DateTimeOffset? now = null)
    {
        var keys = ProductionKeyLoader.ParseKeySet(Read(PublicKeysFile));
        Assert.Single(keys);
        return new LicenseVerifier(keys, new FixedTimeProvider(now ?? Clock));
    }

    [Fact]
    public void Vector_files_match_their_pinned_hashes()
    {
        foreach (var (name, expected) in InteropVectorHashes.Expected)
            Assert.Equal(expected, Convert.ToHexString(SHA256.HashData(Read(name))).ToLowerInvariant());
    }

    [Fact]
    public void Valid_vector_is_Valid_with_the_expected_details()
    {
        var result = Verifier().Verify(Read("valid.lic"));

        Assert.Equal(LicenseStatus.Valid, result.Status);
        Assert.Equal("LIC-VECTOR-VALID", result.Payload!.LicenseId);
        Assert.Equal("Interop Test Customer", result.Payload.Licensee);
        Assert.Equal("FlexiCore", result.Payload.Product);
        Assert.Equal(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), result.Payload.ExpiresAt);
        Assert.Equal(1, result.Payload.MaxInstallations);
    }

    [Fact]
    public void Valid_vector_follows_the_clock()
    {
        Assert.Equal(LicenseStatus.NotYetValid, Verifier(new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)).Verify(Read("valid.lic")).Status);
        Assert.Equal(LicenseStatus.Expired, Verifier(new DateTimeOffset(2027, 6, 1, 0, 0, 0, TimeSpan.Zero)).Verify(Read("valid.lic")).Status);
    }

    [Fact]
    public void Expired_vector_is_Expired() =>
        Assert.Equal(LicenseStatus.Expired, Verifier().Verify(Read("expired.lic")).Status);

    [Fact]
    public void Wrong_product_vector_is_WrongProduct() =>
        Assert.Equal(LicenseStatus.WrongProduct, Verifier().Verify(Read("wrong-product.lic")).Status);

    [Fact]
    public void Tampered_vector_is_InvalidSignature() =>
        Assert.Equal(LicenseStatus.InvalidSignature, Verifier().Verify(Read("tampered.lic")).Status);

    [Fact]
    public void The_vector_key_is_a_test_key_and_is_not_in_the_production_key_set()
    {
        var vectorKeys = ProductionKeyLoader.ParseKeySet(Read(PublicKeysFile));
        Assert.All(vectorKeys, k => Assert.StartsWith("interop-test", k.KeyId, StringComparison.Ordinal));

        foreach (var production in ProductionKeyLoader.Load())
        {
            Assert.DoesNotContain("interop-test", production.KeyId, StringComparison.Ordinal);
            Assert.DoesNotContain(vectorKeys, k => k.SubjectPublicKeyInfo.AsSpan().SequenceEqual(production.SubjectPublicKeyInfo));
        }
    }

    [Fact]
    public void The_vector_folder_holds_only_public_data()
    {
        var expected = new[] { ".gitattributes", "README.md", "interop-test-public-keys.json", "valid.lic", "expired.lic", "wrong-product.lic", "tampered.lic" }
            .Order(StringComparer.Ordinal).ToArray();
        var actual = Directory.GetFiles(VectorDir()).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], actual[i]);

        foreach (var file in Directory.GetFiles(VectorDir()))
            Assert.DoesNotContain("PRIVATE KEY", File.ReadAllText(file), StringComparison.Ordinal);
    }
}