using FlexiCore.ActivationEngine.Services;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Tests;

/// <summary>
/// The Key Management screen only catches LicenseToolException, IOException and UnauthorizedAccessException.
/// These tests check that a bad ERP key file can never surface as any other exception type, and that a damaged
/// ERP file never makes a signing key eligible.
/// </summary>
public sealed class ErpKeyFileRobustnessTests
{
    private const string KeyId = "robust-key-1";

    private static KeyManagementService Manager(Owner o) =>
        new(o.Paths, o.Keys, o.Time, () => null); // never discovers the real ERP key file

    public static TheoryData<string, byte[]> BadFiles() => new()
    {
        { "empty", Array.Empty<byte>() },
        { "broken-json", "{"u8.ToArray() },
        { "not-json", "hello world"u8.ToArray() },
        { "array-root", "[]"u8.ToArray() },
        { "no-keys", "{\"keys\":[]}"u8.ToArray() },
        { "bad-base64", "{\"keys\":[{\"keyId\":\"x\",\"publicKey\":\"!!!\"}]}"u8.ToArray() },
        { "binary", new byte[] { 0xFF, 0xFE, 0x00, 0x01, 0x80, 0x90, 0x00 } },
    };

    [Theory]
    [MemberData(nameof(BadFiles))]
    public void A_bad_ERP_key_file_is_refused_with_LicenseToolException_and_changes_nothing(string name, byte[] content)
    {
        using var o = new Owner();
        var manager = Manager(o);
        var path = Path.Combine(o.Root, "bad-" + name + ".json");
        File.WriteAllBytes(path, content);

        var ex = Record.Exception(() => manager.SetErpKeyFile(path));

        Assert.NotNull(ex);
        Assert.True(ex is LicenseToolException, name + " threw " + ex!.GetType().FullName + ": " + ex.Message);
        Assert.Null(manager.Load().ErpKeyFile);
    }

    [Fact]
    public void A_directory_a_missing_file_and_a_relative_path_are_refused_with_expected_exception_types()
    {
        using var o = new Owner();
        var manager = Manager(o);

        foreach (var path in new[] { o.Root, Path.Combine(o.Root, "does-not-exist.json"), "relative.json", "" })
        {
            var ex = Record.Exception(() => manager.SetErpKeyFile(path));
            Assert.NotNull(ex);
            Assert.True(
                ex is LicenseToolException or IOException or UnauthorizedAccessException,
                "'" + path + "' threw " + ex!.GetType().FullName + ": " + ex.Message);
        }
        Assert.Null(manager.Load().ErpKeyFile);
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("delete")]
    public void A_damaged_ERP_file_after_setup_never_makes_a_key_selectable_and_never_throws(string how)
    {
        using var o = new Owner();
        o.Keys.Create(KeyId, Owner.Pass, Owner.Pass);
        var erp = Path.Combine(o.Root, "erp-keys.json");
        o.Keys.ExportKeySet(new[] { KeyId }, erp);
        var manager = Manager(o);
        manager.SetErpKeyFile(erp);
        Assert.True(manager.Load().IsSelectable(KeyId));

        if (how == "corrupt") File.WriteAllText(erp, "{ not valid");
        else File.Delete(erp);

        var overview = manager.Load();

        Assert.False(overview.IsSelectable(KeyId));
        Assert.NotNull(overview.ErpFileProblem);
        Assert.Throws<LicenseToolException>(() => o.Issue.Issue(o.Input(KeyId)));
        Assert.False(File.Exists(o.Paths.LedgerPath));
    }
}
