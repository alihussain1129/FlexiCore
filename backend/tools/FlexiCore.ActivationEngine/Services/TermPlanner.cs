namespace FlexiCore.ActivationEngine.Services;

public enum TermKind { OneMonth, ThreeMonths, SixMonths, OneYear, Custom, Lifetime }

public sealed record TermChoice(TermKind Kind, string Label);

/// <summary>
/// Calendar-correct license terms. Works on UTC dates; the license then expires at the end of the resulting date
/// (23:59:59 UTC), the same rule the Issue page already used. Month ends clamp (Jan 31 + 1 month = Feb 28/29).
/// Custom and Lifetime have no preset date: Custom uses the date the owner picks, Lifetime means no expiry.
/// </summary>
public static class TermPlanner
{
    public static IReadOnlyList<TermChoice> Choices { get; } =
    [
        new(TermKind.OneMonth, "1 month"),
        new(TermKind.ThreeMonths, "3 months"),
        new(TermKind.SixMonths, "6 months"),
        new(TermKind.OneYear, "1 year"),
        new(TermKind.Custom, "Custom end date"),
        new(TermKind.Lifetime, "Lifetime (never expires)"),
    ];

    public static string Label(TermKind kind) => Choices.First(c => c.Kind == kind).Label;

    /// <summary>The expiry date for a preset term, or null for Custom and Lifetime.</summary>
    public static DateTime? ExpiryDate(TermKind kind, DateTime fromDate)
    {
        var d = fromDate.Date;
        return kind switch
        {
            TermKind.OneMonth => d.AddMonths(1),
            TermKind.ThreeMonths => d.AddMonths(3),
            TermKind.SixMonths => d.AddMonths(6),
            TermKind.OneYear => d.AddYears(1),
            _ => null,
        };
    }
}