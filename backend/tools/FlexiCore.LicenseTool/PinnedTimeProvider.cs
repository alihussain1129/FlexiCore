namespace FlexiCore.LicenseTool;

/// <summary>A clock fixed at one instant. Used for self-checks and for verifying "as of" a date.</summary>
public sealed class PinnedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}