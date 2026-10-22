using System.Globalization;

namespace FlexiCore.ActivationEngine.Services;

public static class Fmt
{
    public static string Time(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";

    public static string Expiry(DateTimeOffset? value) => value is { } v ? Time(v) : "Never";

    public static string Short(string text, int length = 12) =>
        text.Length <= length ? text : text.Substring(0, length) + "\u2026";

    public static string Status(TermStatus status) => status switch
    {
        TermStatus.NotYetValid => "Not yet valid",
        TermStatus.Active => "Active",
        TermStatus.ActiveInGrace => "Active (grace)",
        TermStatus.Expired => "Expired",
        _ => "Unknown",
    };
}
