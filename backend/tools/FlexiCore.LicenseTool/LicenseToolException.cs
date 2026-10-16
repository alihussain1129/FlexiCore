namespace FlexiCore.LicenseTool;

/// <summary>An expected, user-facing failure. The message is safe to print and never contains key material.</summary>
public sealed class LicenseToolException(string message, int exitCode = 1) : Exception(message)
{
    public int ExitCode { get; } = exitCode;
}