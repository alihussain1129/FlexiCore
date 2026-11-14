namespace FlexiCore.ArchitectureTests;

public sealed class ActivationServicesRuleTests
{
    [Fact]
    public void Shared_activation_services_target_plain_net10_without_wpf()
    {
        var root = BackendRoot();
        var project = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "FlexiCore.ActivationServices",
            "FlexiCore.ActivationServices.csproj"));

        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<UseWPF>true</UseWPF>", project, StringComparison.Ordinal);

        var engineRoot = Path.Combine(root, "tools", "FlexiCore.ActivationEngine");
        var sharedFiles = Directory.EnumerateFiles(
                Path.Combine(engineRoot, "Services"),
                "*.cs")
            .Where(path => !Path.GetFileName(path).Equals(
                "WpfDialogService.cs",
                StringComparison.Ordinal));
        sharedFiles = sharedFiles.Concat(
            Directory.EnumerateFiles(
                Path.Combine(engineRoot, "ViewModels"),
                "*.cs"));

        foreach (var file in sharedFiles)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("System.Windows.Controls", source, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Windows.Threading", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Microsoft.Win32", source, StringComparison.Ordinal);
        }
    }

    private static string BackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FlexiCore.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
