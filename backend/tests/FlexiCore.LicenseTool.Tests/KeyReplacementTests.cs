namespace FlexiCore.LicenseTool.Tests;

public sealed class KeyReplacementTests
{
    private const string Pass = "correct horse battery staple";
    private const string Id = "fc-test-a";

    private static bool AllowAll(string _) => true;

    private static Action<string, string, bool> FailWhenMovingTo(string destination) =>
        (source, target, overwrite) =>
        {
            if (string.Equals(target, destination, StringComparison.Ordinal))
                throw new IOException("simulated failure");
            File.Move(source, target, overwrite);
        };

    private static void AssertOnlyTheKeyPairRemains(string directory)
    {
        var names = Directory.GetFiles(directory).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { Id + ".p8", Id + ".public.json" }, names);
    }

    [Fact]
    public void Forced_replacement_keeps_the_old_pair_when_the_public_file_cannot_be_placed()
    {
        using var dir = new TempDir();
        KeyStore.CreateForTesting(Id, dir.Path, Pass, false, AllowAll);
        var privatePath = dir.Combine(Id + ".p8");
        var publicPath = dir.Combine(Id + ".public.json");
        var oldPrivate = File.ReadAllBytes(privatePath);
        var oldPublic = File.ReadAllBytes(publicPath);

        Assert.Throws<IOException>(() =>
            KeyStore.CreateForTesting(Id, dir.Path, Pass, true, AllowAll, FailWhenMovingTo(publicPath)));

        Assert.Equal(oldPrivate, File.ReadAllBytes(privatePath));
        Assert.Equal(oldPublic, File.ReadAllBytes(publicPath));
        AssertOnlyTheKeyPairRemains(dir.Path);
    }

    [Fact]
    public void Forced_replacement_keeps_the_old_pair_when_the_private_file_cannot_be_placed()
    {
        using var dir = new TempDir();
        KeyStore.CreateForTesting(Id, dir.Path, Pass, false, AllowAll);
        var privatePath = dir.Combine(Id + ".p8");
        var publicPath = dir.Combine(Id + ".public.json");
        var oldPrivate = File.ReadAllBytes(privatePath);
        var oldPublic = File.ReadAllBytes(publicPath);

        Assert.Throws<IOException>(() =>
            KeyStore.CreateForTesting(Id, dir.Path, Pass, true, AllowAll, FailWhenMovingTo(privatePath)));

        Assert.Equal(oldPrivate, File.ReadAllBytes(privatePath));
        Assert.Equal(oldPublic, File.ReadAllBytes(publicPath));
        AssertOnlyTheKeyPairRemains(dir.Path);
    }

    [Fact]
    public void A_failed_first_time_creation_leaves_nothing_behind()
    {
        using var dir = new TempDir();
        var publicPath = dir.Combine(Id + ".public.json");

        Assert.Throws<IOException>(() =>
            KeyStore.CreateForTesting(Id, dir.Path, Pass, false, AllowAll, FailWhenMovingTo(publicPath)));

        Assert.Empty(Directory.GetFiles(dir.Path));
    }

    [Fact]
    public void Successful_forced_replacement_swaps_both_files_and_leaves_no_temporary_files()
    {
        using var dir = new TempDir();
        KeyStore.CreateForTesting(Id, dir.Path, Pass, false, AllowAll);
        var oldPrivate = File.ReadAllBytes(dir.Combine(Id + ".p8"));
        var oldPublic = File.ReadAllBytes(dir.Combine(Id + ".public.json"));

        var created = KeyStore.CreateForTesting(Id, dir.Path, Pass, true, AllowAll);

        Assert.NotEqual(oldPrivate, File.ReadAllBytes(created.PrivateKeyPath));
        Assert.NotEqual(oldPublic, File.ReadAllBytes(created.PublicKeyPath));
        using var signer = KeyStore.Load(created.PrivateKeyPath, Pass);
        Assert.Equal(created.Fingerprint, KeyStore.Fingerprint(signer.SubjectPublicKeyInfo));
        AssertOnlyTheKeyPairRemains(dir.Path);
    }
}