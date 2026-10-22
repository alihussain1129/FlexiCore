using System.Runtime.InteropServices;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.ViewModels;

public sealed class SettingsViewModel : PageViewModel
{
    public SettingsViewModel(OwnerPaths paths)
        : base("Settings", "Where data lives and which licensing contract this app uses.", "\uE713")
    {
        var version = typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        Details = new List<DetailRow>
        {
            new DetailRow("Application", "FlexiCore Activation Engine " + version),
            new DetailRow("Runtime", RuntimeInformation.FrameworkDescription),
            new DetailRow("Owner data folder", paths.Root),
            new DetailRow("Signing keys folder", paths.KeysDirectory),
            new DetailRow("Issue ledger", paths.LedgerPath),
            new DetailRow("Folder safety",
                (paths.InsideGitWorkingTree ? "INSIDE a git working tree (move it). " : "Outside any git working tree. ")
                + (paths.LooksCloudSynced ? "Looks cloud-synced (move it)." : "No cloud-sync folder detected.")),
            new DetailRow("License timestamp format", LicenseIssuer.TimestampFormat),
            new DetailRow("Signature scheme", "ES256 (ECDSA P-256 / SHA-256), raw 64-byte r||s, base64url without padding"),
            new DetailRow("Licensing contract", "Reused from FlexiCore.LicenseTool and FlexiCore.Licensing by project reference. Nothing is re-implemented in this app."),
            new DetailRow("Private keys", "Stay in the encrypted key files. Never displayed, logged or exported by this application."),
        };
    }

    public IReadOnlyList<DetailRow> Details { get; }
}
