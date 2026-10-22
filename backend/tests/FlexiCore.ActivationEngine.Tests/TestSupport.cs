using FlexiCore.ActivationEngine.Services;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Tests;

internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fc-engine-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }
    public string Combine(string name) => System.IO.Path.Combine(Root, name);

    public void Dispose()
    {
        try { Directory.Delete(Root, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

internal sealed class FakeDialogs : IDialogService
{
    public string? SavePath { get; set; }
    public string? OpenPath { get; set; }
    public bool ConfirmResult { get; set; } = true;
    public int ConfirmCalls { get; private set; }
    public string? RevealedPath { get; private set; }

    public string? PickSavePath(string title, string defaultFileName, string filter) => SavePath;
    public string? PickOpenPath(string title, string filter) => OpenPath;
    public bool Confirm(string title, string message) { ConfirmCalls++; return ConfirmResult; }
    public void Reveal(string filePath) => RevealedPath = filePath;
    public void OpenFolder(string folderPath) { }
}

/// <summary>One throwaway owner environment: temp data folder, test-only keys, pinned clock. Never touches real owner data.</summary>
internal sealed class Owner : IDisposable
{
    public const string Pass = "correct horse battery staple";
    public static readonly DateTimeOffset Now = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

    private readonly TempDir _dir = new();

    public Owner()
    {
        Paths = new OwnerPaths(_dir.Combine("owner"));
        Keys = new KeyService(Paths);
        Time = new PinnedTimeProvider(Now);
        Issue = new IssueService(Paths, Time);
        OutDir = _dir.Combine("out");
        Directory.CreateDirectory(OutDir);
    }

    public OwnerPaths Paths { get; }
    public KeyService Keys { get; }
    public TimeProvider Time { get; }
    public IssueService Issue { get; }
    public string OutDir { get; }
    public string Root => _dir.Root;

    public string Out(string name) => System.IO.Path.Combine(OutDir, name);

    public string CreateKey(string id = "test-key-1")
    {
        Keys.Create(id, Pass, Pass);
        return id;
    }

    public IssueInput Input(
        string keyId, string licenseId = "FC-TEST-0001", string licensee = "Test Customer",
        string? installationId = null, string? output = null, int grace = 0, int installs = 1,
        DateTimeOffset? expires = null, bool perpetual = false, bool code = false)
    {
        var start = LicenseIssuer.Truncate(Now);
        DateTimeOffset? end = perpetual ? null : (expires ?? start.AddYears(1));
        return new IssueInput(keyId, Pass, licenseId, licensee, start, end, grace, installs, installationId,
            output ?? Out(licenseId + ".lic"), code);
    }

    public LicenseVerificationResult Verify(string keyId, string licensePath)
    {
        var trusted = PublicKeyExport.Read(System.IO.Path.Combine(Paths.KeysDirectory, keyId + ".public.json"));
        return new LicenseVerifier(trusted, new PinnedTimeProvider(Now)).Verify(File.ReadAllBytes(licensePath));
    }

    public void Dispose() => _dir.Dispose();
}
