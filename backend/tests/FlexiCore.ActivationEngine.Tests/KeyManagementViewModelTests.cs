using FlexiCore.ActivationEngine.Services;
using FlexiCore.ActivationEngine.ViewModels;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class KeyManagementViewModelTests
{
    private static KeyManagementViewModel Page(Owner o) =>
        new(new KeyManagementService(o.Paths, o.Keys, o.Time, () => null), new FakeDialogs(), o.Time);

    [Theory]
    [InlineData("short", "short")]
    [InlineData("correct horse battery staple", "a different confirmation")]
    public async Task A_failed_key_creation_reports_failure_so_the_passphrases_are_not_cleared(string pass, string confirm)
    {
        using var o = new Owner();
        var page = Page(o);

        var used = await page.CreateKeyAsync(pass, confirm);

        Assert.False(used);
        Assert.Equal(StatusKind.Error.ToString(), page.StatusKindName);
        Assert.Empty(o.Keys.List(out _));
    }

    [Fact]
    public async Task A_successful_key_creation_reports_success_and_creates_one_key()
    {
        using var o = new Owner();
        var page = Page(o);

        var used = await page.CreateKeyAsync(Owner.Pass, Owner.Pass);

        Assert.True(used);
        Assert.Equal(StatusKind.Success.ToString(), page.StatusKindName);
        Assert.Single(o.Keys.List(out _));
    }
}
