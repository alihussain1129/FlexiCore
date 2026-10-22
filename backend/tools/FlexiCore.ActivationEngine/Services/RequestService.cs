using System.Text.RegularExpressions;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Services;

public sealed record ParsedRequest(string InstallationId, string RequestCode, string ShortId);

/// <summary>Parses customer request codes with the existing InstallationIds implementation. No second format exists.</summary>
public static class RequestService
{
    private static readonly Regex CodeInText = new(
        @"FCR1(?:[\s-]*[0-9A-Fa-f]{4}){17}", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static ParsedRequest Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new LicenseToolException("Paste a request code first.");
        if (text.Length > 4000) throw new LicenseToolException("That is too long to be a request code.");

        if (!InstallationIds.TryParseRequestCode(text, out var id))
        {
            var match = CodeInText.Match(text);
            if (!match.Success || !InstallationIds.TryParseRequestCode(match.Value, out id))
                throw new LicenseToolException(
                    "This is not a valid FlexiCore request code (expected FCR1-... with a correct checksum). "
                    + "It may be mistyped, truncated or altered.");
        }

        return new ParsedRequest(id, InstallationIds.ToRequestCode(id), id.Substring(0, 12));
    }

    public static ParsedRequest ParseFile(string path) => Parse(SafeFile.ReadTextLimited(path, 4096));
}
