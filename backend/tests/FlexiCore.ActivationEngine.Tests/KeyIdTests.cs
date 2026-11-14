using System.IO;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.LicenseTool;
using Xunit;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class KeyIdTests : IDisposable
{
    private const string Passphrase = "Tr0ub4dor&3-horse-staple-battery-9";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "fc-keyid-" + Guid.NewGuid().ToString("N"));
    private readonly OwnerPaths _paths;
    private readonly KeyService _keys;
    private readonly KeyManagementService _service;

    public KeyIdTests()
    {
        Directory.CreateDirectory(_root);
        _paths = new OwnerPaths(Path.Combine(_root, "home"));
        _keys = new KeyService(_paths);
        _service = new KeyManagementService(_paths, _keys, TimeProvider.System, () => null);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void A_used_key_id_is_refused()
    {
        _keys.Create("fc-2026-a", Passphrase, Passphrase);

        var ex = Assert.Throws<LicenseToolException>(() => _service.EnsureCanCreate("fc-2026-a"));

        Assert.Contains("already used", ex.Message);
    }

    [Fact]
    public void A_manually_created_key_is_logged_and_waits_for_the_erp()
    {
        _keys.Create("fc-2026-a", Passphrase, Passphrase);

        _service.RecordManualKey("fc-2026-a", false);

        Assert.True(_service.Load().Keys.Single(k => k.KeyId == "fc-2026-a").PendingErp);
        Assert.Contains(_service.ReadEvents(5), e => e.Action == "created" && e.KeyId == "fc-2026-a");
    }

    [Fact]
    public void A_test_key_is_logged_but_not_held_back()
    {
        _keys.Create("dev-test-1", Passphrase, Passphrase);

        _service.RecordManualKey("dev-test-1", true);

        Assert.False(_service.Load().Keys.Single(k => k.KeyId == "dev-test-1").PendingErp);
        Assert.Contains(_service.ReadEvents(5), e => e.KeyId == "dev-test-1");
    }
}