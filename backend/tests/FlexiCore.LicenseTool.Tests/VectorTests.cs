using FlexiCore.Licensing;

namespace FlexiCore.LicenseTool.Tests;

public sealed class VectorTests
{
    private static int Run(FakeConsole console, params string[] args) =>
        LicenseToolApp.Run(args, console, new PinnedTimeProvider(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void Vectors_have_the_expected_statuses_and_contain_no_private_key()
    {
        using var dir = new TempDir();
        Assert.Equal(0, Run(new FakeConsole(), "make-test-vectors", "--out", dir.Path));

        foreach (var name in TestVectors.FileNames) Assert.True(File.Exists(dir.Combine(name)), name);

        var pub = dir.Combine(TestVectors.PublicKeysFile);
        Assert.Equal(0, Run(new FakeConsole(), "verify", dir.Combine("valid.lic"), "--public-keys", pub, "--at", "2026-06-01"));

        var expired = new FakeConsole();
        Assert.Equal(3, Run(expired, "verify", dir.Combine("expired.lic"), "--public-keys", pub, "--at", "2026-06-01"));
        Assert.Contains("Expired", expired.AllOutput, StringComparison.Ordinal);

        var wrong = new FakeConsole();
        Assert.Equal(3, Run(wrong, "verify", dir.Combine("wrong-product.lic"), "--public-keys", pub, "--at", "2026-06-01"));
        Assert.Contains("WrongProduct", wrong.AllOutput, StringComparison.Ordinal);

        var tampered = new FakeConsole();
        Assert.Equal(3, Run(tampered, "verify", dir.Combine("tampered.lic"), "--public-keys", pub, "--at", "2026-06-01"));
        Assert.Contains("InvalidSignature", tampered.AllOutput, StringComparison.Ordinal);

        foreach (var file in Directory.GetFiles(dir.Path))
            Assert.DoesNotContain("PRIVATE KEY", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void The_vector_key_set_holds_only_the_reserved_test_key_id()
    {
        using var dir = new TempDir();
        Assert.Equal(0, Run(new FakeConsole(), "make-test-vectors", "--out", dir.Path));

        var keys = ProductionKeyLoader.ParseKeySet(File.ReadAllBytes(dir.Combine(TestVectors.PublicKeysFile)));

        Assert.Single(keys);
        Assert.Equal("interop-test-1", keys[0].KeyId);
    }

    [Fact]
    public void Existing_vectors_are_not_overwritten_without_force()
    {
        using var dir = new TempDir();
        Assert.Equal(0, Run(new FakeConsole(), "make-test-vectors", "--out", dir.Path));
        var before = File.ReadAllBytes(dir.Combine("valid.lic"));

        Assert.Equal(1, Run(new FakeConsole(), "make-test-vectors", "--out", dir.Path));
        Assert.Equal(before, File.ReadAllBytes(dir.Combine("valid.lic")));

        Assert.Equal(0, Run(new FakeConsole(), "make-test-vectors", "--out", dir.Path, "--force"));
        Assert.NotEqual(before, File.ReadAllBytes(dir.Combine("valid.lic")));
    }
}