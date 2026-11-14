using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Services;

/// <summary>Where the owner's data lives. Always outside any git working tree (checked, not assumed).</summary>
public sealed class OwnerPaths
{
    public OwnerPaths(string? root = null)
    {
        var overrideRoot = Environment.GetEnvironmentVariable("FLEXICORE_ENGINE_HOME");
        if (root is not null)
            Root = root;
        else if (!string.IsNullOrWhiteSpace(overrideRoot) && Path.IsPathFullyQualified(overrideRoot))
            Root = Path.GetFullPath(overrideRoot);
        else
            Root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlexiCoreActivationEngine");

        KeysDirectory = Path.Combine(Root, "keys");
        LedgerPath = Path.Combine(Root, "issue-ledger.jsonl");
    }

    public string Root { get; }
    public string KeysDirectory { get; }
    public string LedgerPath { get; }

    public bool LooksCloudSynced => OutputPathGuard.LooksCloudSynced(Root);
    public bool InsideGitWorkingTree => OutputPathGuard.IsInsideGitWorkingTree(Root);
}
