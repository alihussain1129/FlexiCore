using FlexiCore.Licensing;

namespace FlexiCore.UnitTests.LicensingTests;

public sealed class MonotonicClockTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "flexicore-clock-" + Guid.NewGuid().ToString("N"));

    public MonotonicClockTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private string LicensePath => Path.Combine(_dir, FileLicenseStore.FileName);
    private string MarkerPath => Path.Combine(_dir, MonotonicClock.FileName);

    [Fact]
    public void Never_goes_backwards_within_a_run()
    {
        var inner = new FixedTimeProvider(T0.AddDays(10));
        var clock = new MonotonicClock(inner, _dir);

        Assert.Equal(T0.AddDays(10), clock.GetUtcNow());
        inner.Now = T0;
        Assert.Equal(T0.AddDays(10), clock.GetUtcNow());
        inner.Now = T0.AddDays(11);
        Assert.Equal(T0.AddDays(11), clock.GetUtcNow());
    }

    [Fact]
    public void Remembers_the_latest_time_across_a_restart_once_a_license_exists()
    {
        File.WriteAllBytes(LicensePath, new byte[] { 1 });
        var first = new MonotonicClock(new FixedTimeProvider(T0.AddDays(30)), _dir);
        Assert.Equal(T0.AddDays(30), first.GetUtcNow());
        Assert.True(File.Exists(MarkerPath));

        var restarted = new MonotonicClock(new FixedTimeProvider(T0), _dir);

        Assert.Equal(T0.AddDays(30), restarted.GetUtcNow());
    }

    [Fact]
    public void Writes_nothing_while_no_license_is_stored()
    {
        var clock = new MonotonicClock(new FixedTimeProvider(T0), _dir);

        clock.GetUtcNow();

        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public void Persists_immediately_when_a_license_is_added_after_startup()
    {
        var clock = new MonotonicClock(new FixedTimeProvider(T0), _dir);

        clock.GetUtcNow();
        Assert.Empty(Directory.GetFiles(_dir));

        File.WriteAllBytes(LicensePath, new byte[] { 1 });

        Assert.Equal(T0, clock.GetUtcNow());
        Assert.True(File.Exists(MarkerPath));
    }

    [Fact]
    public void Retries_marker_persistence_after_a_write_failure()
    {
        File.WriteAllBytes(LicensePath, new byte[] { 1 });
        Directory.CreateDirectory(MarkerPath);

        var now = T0.AddMinutes(2);
        var clock = new MonotonicClock(new FixedTimeProvider(now), _dir);

        clock.GetUtcNow();
        Assert.True(Directory.Exists(MarkerPath));

        Directory.Delete(MarkerPath);

        Assert.Equal(now, clock.GetUtcNow());
        Assert.True(File.Exists(MarkerPath));
    }

    [Theory]
    [InlineData("not a date")]
    [InlineData("")]
    [InlineData("2026-06-01T00:00:00.0000000Z-and-a-lot-of-extra-text-so-that-the-marker-is-larger-than-the-limit-of-64-bytes")]
    public void A_damaged_marker_is_ignored_and_never_throws(string content)
    {
        File.WriteAllBytes(LicensePath, new byte[] { 1 });
        File.WriteAllText(MarkerPath, content);

        var clock = new MonotonicClock(new FixedTimeProvider(T0), _dir);

        Assert.Equal(T0, clock.GetUtcNow());
    }

    [Fact]
    public void Rolling_the_clock_back_cannot_revive_an_expired_license_but_the_license_is_kept()
    {
        using var signer = new TestSigner();
        var file = signer.Build(TestSigner.ValidPayload); // expires 2027-01-01, no grace
        File.WriteAllBytes(LicensePath, file);
        var afterExpiry = new DateTimeOffset(2027, 2, 1, 0, 0, 0, TimeSpan.Zero);

        var before = new LicenseVerifier(new[] { signer.Trusted() }, new MonotonicClock(new FixedTimeProvider(afterExpiry), _dir));
        Assert.Equal(LicenseStatus.Expired, before.Verify(file).Status);

        // The computer is restarted with its clock set back to a time when the license was still valid.
        var rolledBack = new LicenseVerifier(new[] { signer.Trusted() }, new MonotonicClock(new FixedTimeProvider(T0), _dir));
        Assert.Equal(LicenseStatus.Expired, rolledBack.Verify(file).Status);
        Assert.Equal(file, File.ReadAllBytes(LicensePath));

        // Without the protection the same rollback would be accepted.
        var unprotected = new LicenseVerifier(new[] { signer.Trusted() }, new FixedTimeProvider(T0));
        Assert.Equal(LicenseStatus.Valid, unprotected.Verify(file).Status);
    }
}
