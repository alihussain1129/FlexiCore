using System.Globalization;
using System.Text;

namespace FlexiCore.Licensing;

/// <summary>
/// Clock for license checks that never reports a time earlier than the latest time it has seen, including before
/// a restart. The latest time is kept in a small marker file next to the license, and only once a license is stored.
/// This stops casual clock rollback; it is not tamper-proof against someone who can delete the marker, just as the
/// license file itself is signed, not secret. It never deletes anything and never locks out a license that is
/// still valid. To recover from a clock that was once set far into the future, delete the marker file.
/// </summary>
public sealed class MonotonicClock : TimeProvider
{
    public const string FileName = "clock.hwm";
    private const int MaxMarkerBytes = 64;
    private static readonly TimeSpan PersistEvery = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _inner;
    private readonly string _directory;
    private readonly object _gate = new();
    private DateTimeOffset _highWater;
    private DateTimeOffset _persisted;

    public MonotonicClock(TimeProvider inner, string directory)
    {
        ArgumentNullException.ThrowIfNull(inner);
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            throw new ArgumentException("The clock directory must be an absolute path.", nameof(directory));
        _inner = inner;
        _directory = directory;
        _highWater = ReadStored() ?? DateTimeOffset.MinValue;
        _persisted = _highWater;
    }

    private string MarkerPath => Path.Combine(_directory, FileName);

    public override DateTimeOffset GetUtcNow()
    {
        var now = _inner.GetUtcNow();
        lock (_gate)
        {
            if (now > _highWater) _highWater = now;
            if (_highWater - _persisted >= PersistEvery && Persist())
                _persisted = _highWater;
            return _highWater;
        }
    }

    private bool Persist()
    {
        // Do not mark the time as persisted until a license exists and the marker is written.
        if (!File.Exists(Path.Combine(_directory, FileLicenseStore.FileName)))
            return false;

        var temp = MarkerPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, _highWater.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), Encoding.ASCII);
            File.Move(temp, MarkerPath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(temp); }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
            return false;
        }
    }

    private DateTimeOffset? ReadStored()
    {
        try
        {
            var info = new FileInfo(MarkerPath);
            if (!info.Exists || info.Length is 0 or > MaxMarkerBytes) return null;
            var text = File.ReadAllText(MarkerPath, Encoding.ASCII).Trim();
            return DateTimeOffset.TryParse(
                text, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value)
                ? value
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
