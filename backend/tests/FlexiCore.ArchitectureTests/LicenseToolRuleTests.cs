using System.Text.RegularExpressions;
using Xunit;

namespace FlexiCore.ArchitectureTests;

/// <summary>Keeps the owner tool and every private key out of the product, the repository and the build output.</summary>
public class LicenseToolRuleTests
{
    private static readonly string[] SkippedFolders = ["bin", "obj", ".vs", ".git", "node_modules"];

    private static readonly string[] TextExtensions =
    [
        ".cs", ".csproj", ".json", ".md", ".txt", ".lic", ".xml", ".yml", ".yaml", ".props", ".targets",
        ".slnx", ".ps1", ".sh", ".config", ".p8", ".pem", ".key", ".enc",
    ];

    private static readonly string[] KeyFileExtensions = [".p8", ".pem", ".key", ".pfx", ".enc"];

    private static readonly Regex PrivateKeyBlock = new(
        @"-----BEGIN [A-Z ]*PRIVATE KEY-----\s*[A-Za-z0-9+/=]{40,}", RegexOptions.CultureInvariant);

    private static string BackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FlexiCore.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static IEnumerable<string> Files(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var segments = Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(s => SkippedFolders.Contains(s, StringComparer.OrdinalIgnoreCase))) continue;
            yield return file;
        }
    }

    private static string Relative(string root, string file) =>
        Path.GetRelativePath(root, file).Replace('\\', '/');

    [Fact]
    public void Nothing_references_the_license_tool_except_its_own_tests()
    {
        var root = BackendRoot();
        foreach (var project in Files(root).Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
        {
            var relative = Relative(root, project);
            if (relative.StartsWith("tools/FlexiCore.LicenseTool/", StringComparison.Ordinal)
                || relative.StartsWith("tests/FlexiCore.LicenseTool.Tests/", StringComparison.Ordinal))
                continue;

            Assert.DoesNotContain("FlexiCore.LicenseTool", File.ReadAllText(project), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_tool_has_no_package_dependencies()
    {
        var text = File.ReadAllText(Path.Combine(BackendRoot(), "tools", "FlexiCore.LicenseTool", "FlexiCore.LicenseTool.csproj"));
        Assert.DoesNotContain("PackageReference", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_code_has_no_signing_or_private_key_api()
    {
        var forbidden = new[]
        {
            "SignData", "SignHash", "ExportPkcs8PrivateKey", "ExportEncryptedPkcs8PrivateKey", "ExportECPrivateKey",
            "ImportPkcs8PrivateKey", "ImportEncryptedPkcs8PrivateKey", "ImportECPrivateKey", "PRIVATE KEY",
        };
        var root = BackendRoot();
        foreach (var project in new[] { "FlexiCore.Licensing", "FlexiCore.Api" })
        {
            foreach (var file in Files(Path.Combine(root, "src", project)).Where(f => f.EndsWith(".cs", StringComparison.Ordinal)))
            {
                var text = File.ReadAllText(file);
                foreach (var token in forbidden)
                    Assert.False(text.Contains(token, StringComparison.Ordinal), $"{Relative(root, file)} contains '{token}'");
            }
        }
    }

    [Fact]
    public void No_key_files_or_private_key_blocks_exist_in_the_backend_tree()
    {
        var root = BackendRoot();
        foreach (var file in Files(root))
        {
            var extension = Path.GetExtension(file);
            Assert.False(
                KeyFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase),
                $"Key file in the repository tree: {Relative(root, file)}");

            if (!TextExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) continue;
            Assert.False(
                PrivateKeyBlock.IsMatch(File.ReadAllText(file)),
                $"Private key block found in {Relative(root, file)}");
        }
    }

    [Fact]
    public void The_production_key_set_never_contains_test_keys()
    {
        var path = Path.Combine(BackendRoot(), "src", "FlexiCore.Licensing", "Keys", "production-public-keys.json");
        if (!File.Exists(path)) return;
        Assert.DoesNotContain("interop-test", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void Gitignore_blocks_key_and_ledger_files()
    {
        var gitignore = Path.Combine(Directory.GetParent(BackendRoot())!.FullName, ".gitignore");
        var lines = File.ReadAllLines(gitignore).Select(l => l.Trim()).ToHashSet(StringComparer.Ordinal);
        foreach (var pattern in new[] { "*.p8", "*.pem", "*.key", "*.enc", "*.private.*", "issued*.jsonl" })
            Assert.Contains(pattern, lines);
    }
}