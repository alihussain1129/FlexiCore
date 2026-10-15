using System.Net;
using FlexiCore.IntegrationTests.Infrastructure;
using static FlexiCore.IntegrationTests.SessionTestHelpers;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class SessionRaceLoopTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Logout_racing_refresh_over_many_interleavings_always_returns_204_and_leaves_no_usable_token()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);

        for (var i = 0; i < 25; i++)
        {
            var session = await Scenario.LoginAsync(app);
            var refreshTask = app.RefreshAsync(session.RefreshToken);
            var logoutTask = app.LogoutAsync(session.RefreshToken);
            await Task.WhenAll(refreshTask, logoutTask);
            var refresh = await refreshTask;
            var logout = await logoutTask;

            var context = $"iteration {i}: refresh={(int)refresh.StatusCode}, logout={(int)logout.StatusCode}";

            Assert.True(logout.StatusCode == HttpStatusCode.NoContent,
                context + " (logout must always return 204)" + await DumpAsync(app));
            Assert.True(refresh.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized,
                context + " (refresh must be 200 or 401)" + await DumpAsync(app));

            if (refresh.StatusCode == HttpStatusCode.OK)
            {
                var issued = await Scenario.ReadSessionAsync(refresh);
                var again = await app.RefreshAsync(issued.RefreshToken);
                Assert.True(again.StatusCode == HttpStatusCode.Unauthorized,
                    context + " (a token issued by the racing refresh is still refreshable)" + await DumpAsync(app));
                var me = await app.MeAsync(issued.AccessToken);
                Assert.True(me.StatusCode == HttpStatusCode.Unauthorized,
                    context + " (the access token issued by the racing refresh is still accepted)" + await DumpAsync(app));
            }

            Assert.True(await UnrevokedAsync(app) == 0L,
                context + " (an unrevoked session row remains)" + await DumpAsync(app));
        }
    }
}