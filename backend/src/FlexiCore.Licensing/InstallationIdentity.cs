using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace FlexiCore.Licensing;

public interface IInstallationIdentity
{
    /// <summary>64 lowercase hex characters, or null when this computer's identity cannot be read (fail closed).</summary>
    string? InstallationId { get; }
}

public sealed class MachineInstallationIdentity : IInstallationIdentity
{
    private readonly Lazy<string?> _id = new(Compute);
    public string? InstallationId => _id.Value;

    private static string? Compute()
    {
        var source = ReadMachineSource();
        return string.IsNullOrWhiteSpace(source) ? null : InstallationIds.FromMachineSource(source);
    }

    private static string? ReadMachineSource()
    {
        try
        {
            if (OperatingSystem.IsWindows()) return ReadWindows();
            if (OperatingSystem.IsLinux())
                foreach (var path in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
                    if (File.Exists(path)) return File.ReadAllText(path).Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }
        return null;
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadWindows()
    {
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
        return key?.GetValue("MachineGuid") as string;
    }
}

public static class InstallationIds
{
    private static readonly Regex Pattern = new(
        @"^[0-9a-f]{64}\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static bool IsValid(string? id) => id is not null && Pattern.IsMatch(id);

    public static string FromMachineSource(string source) =>
        Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes("FlexiCore.Installation.v1|" + source.Trim().ToLowerInvariant())));

    private static string Checksum(string id) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("FCR1|" + id)))[..4];

    /// <summary>Customer-facing request code: FCR1-XXXX-XXXX-... (id plus a 4 character checksum, uppercase hex).</summary>
    public static string ToRequestCode(string id)
    {
        if (!IsValid(id)) throw new ArgumentException("Invalid installation id.", nameof(id));
        var body = (id + Checksum(id)).ToUpperInvariant();
        var sb = new StringBuilder("FCR1");
        for (var i = 0; i < body.Length; i += 4) sb.Append('-').Append(body, i, 4);
        return sb.ToString();
    }

    public static bool TryParseRequestCode(string? text, out string id)
    {
        id = "";
        if (string.IsNullOrWhiteSpace(text) || text.Length > 200) return false;
        var clean = new string(text.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();
        if (!clean.StartsWith("FCR1", StringComparison.Ordinal) || clean.Length != 4 + 68) return false;
        var body = clean[4..].ToLowerInvariant();
        var candidate = body[..64];
        if (!IsValid(candidate)) return false;
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(body[64..]), Encoding.ASCII.GetBytes(Checksum(candidate))))
            return false;
        id = candidate;
        return true;
    }
}