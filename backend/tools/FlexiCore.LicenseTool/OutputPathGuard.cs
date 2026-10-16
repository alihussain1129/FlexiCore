namespace FlexiCore.LicenseTool;

/// <summary>Keeps secrets and customer data out of places where they could be committed or synced by accident.</summary>
public static class OutputPathGuard
{
    private static readonly string[] SyncedFolderMarkers = ["OneDrive", "Dropbox", "Google Drive", "iCloud"];

    public static void EnsureAbsolute(string path, string what)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new LicenseToolException($"{what} must be an absolute path.");
    }

    public static bool IsInsideGitWorkingTree(string directory)
    {
        DirectoryInfo? current = new DirectoryInfo(Path.GetFullPath(directory));
        while (current is not null)
        {
            var git = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(git) || File.Exists(git)) return true;
            current = current.Parent;
        }
        return false;
    }

    public static void EnsureOutsideGitWorkingTree(string directory, string what)
    {
        EnsureAbsolute(directory, what);
        if (IsInsideGitWorkingTree(directory))
            throw new LicenseToolException($"{what} is inside a git working tree. Choose a location outside every repository.");
    }

    public static bool LooksCloudSynced(string path) =>
        SyncedFolderMarkers.Any(marker => path.Contains(marker, StringComparison.OrdinalIgnoreCase));
}