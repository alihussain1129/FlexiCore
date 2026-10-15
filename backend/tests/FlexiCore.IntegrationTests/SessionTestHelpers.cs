using FlexiCore.IntegrationTests.Infrastructure;

namespace FlexiCore.IntegrationTests;

internal static class SessionTestHelpers
{
    public static async Task UseRepeatableReadByDefaultAsync(IdentityTestApp app)
    {
        await Sql.ExecAsync(app.Db.OwnerConnectionString,
            $"alter database \"{app.Db.Name}\" set default_transaction_isolation = 'repeatable read'");

        // Guards the test itself: new connections really do default to repeatable read now.
        Assert.Equal("repeatable read", await Sql.ScalarAsync(app.Db.OwnerConnectionString, "show default_transaction_isolation"));
    }

    public static async Task<long> UnrevokedAsync(IdentityTestApp app) =>
        (long)(await Sql.ScalarAsync(app.Db.OwnerConnectionString,
            "select count(*) from identity.refresh_sessions where revoked_at is null"))!;

    public static async Task<string> DumpAsync(IdentityTestApp app)
    {
        var owner = app.Db.OwnerConnectionString;
        var sessions = await Sql.ScalarAsync(owner,
            "select string_agg((rotated_at is not null)::text || '/' || (revoked_at is not null)::text || '/' || coalesce(revoked_reason, '-'), ', ') " +
            "from identity.refresh_sessions");
        var audit = await Sql.ScalarAsync(owner,
            "select string_agg(action || ':' || outcome || ':' || coalesce(metadata->>'reason', '-'), ', ') " +
            "from audit.events where action like 'auth.%'");
        return $"{Environment.NewLine}sessions (rotated/revoked/reason): {sessions}{Environment.NewLine}audit: {audit}";
    }
}