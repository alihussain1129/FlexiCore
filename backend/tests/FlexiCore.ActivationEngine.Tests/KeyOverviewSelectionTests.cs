using FlexiCore.ActivationEngine.Services;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class KeyOverviewSelectionTests
{
    [Fact]
    public void ProductionKey_IsNotSelectable_WhenErpPresenceIsUnknown()
    {
        var overview = CreateOverview(CreateKey("fc-2026-a", inErpFile: null));

        Assert.False(overview.IsSelectable("fc-2026-a"));
    }

    [Fact]
    public void ProductionKey_IsNotSelectable_WhenMissingFromErpFile()
    {
        var overview = CreateOverview(CreateKey("fc-2026-a", inErpFile: false));

        Assert.False(overview.IsSelectable("fc-2026-a"));
    }

    [Fact]
    public void ProductionKey_IsSelectable_WhenConfirmedInErpFile()
    {
        var overview = CreateOverview(CreateKey("fc-2026-a", inErpFile: true));

        Assert.True(overview.IsSelectable("fc-2026-a"));
    }

    [Fact]
    public void TestKey_RemainsSelectable_WhenErpPresenceIsUnknown()
    {
        var overview = CreateOverview(CreateKey("dev-test-1", inErpFile: null));

        Assert.True(overview.IsSelectable("dev-test-1"));
    }

    [Fact]
    public void PendingProductionKey_IsNotSelectable_WhenNotConfirmedInErp()
    {
        var overview = CreateOverview(
            CreateKey("fc-2026-b", pendingErp: true, inErpFile: null));

        Assert.False(overview.IsSelectable("fc-2026-b"));
    }

    [Fact]
    public void RetiredProductionKey_IsNotSelectable_EvenWhenPresentInErp()
    {
        var overview = CreateOverview(
            CreateKey("fc-2026-a", inErpFile: true, retired: true));

        Assert.False(overview.IsSelectable("fc-2026-a"));
    }

    private static ManagedKey CreateKey(
        string keyId,
        bool pendingErp = false,
        bool? inErpFile = null,
        bool retired = false)
    {
        return new ManagedKey(
            keyId,
            DateTimeOffset.UnixEpoch,
            null,
            null,
            retired,
            pendingErp,
            inErpFile,
            0,
            null,
            null,
            0,
            null);
    }

    private static KeyOverview CreateOverview(ManagedKey key) =>
        new(new[] { key }, null, null, null, null, "Test overview");
}
