using System.Globalization;
using FlexiCore.ActivationEngine.Services;

namespace FlexiCore.ActivationEngine.Tests;

public class TermPlannerTests
{
    private static DateTime D(string s) => DateTime.Parse(s, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(TermKind.OneMonth, "2026-10-21", "2026-11-21")]
    [InlineData(TermKind.OneMonth, "2026-01-31", "2026-02-28")]
    [InlineData(TermKind.OneMonth, "2028-01-31", "2028-02-29")]
    [InlineData(TermKind.ThreeMonths, "2026-08-31", "2026-11-30")]
    [InlineData(TermKind.SixMonths, "2026-08-31", "2027-02-28")]
    [InlineData(TermKind.OneYear, "2026-10-21", "2027-10-21")]
    [InlineData(TermKind.OneYear, "2028-02-29", "2029-02-28")]
    public void Preset_terms_use_calendar_arithmetic(TermKind kind, string from, string expected) =>
        Assert.Equal(D(expected), TermPlanner.ExpiryDate(kind, D(from)));

    [Theory]
    [InlineData(TermKind.Custom)]
    [InlineData(TermKind.Lifetime)]
    public void Custom_and_lifetime_have_no_preset_date(TermKind kind) =>
        Assert.Null(TermPlanner.ExpiryDate(kind, D("2026-10-21")));

    [Fact]
    public void Every_kind_has_a_label() =>
        Assert.All(Enum.GetValues<TermKind>(), k => Assert.False(string.IsNullOrWhiteSpace(TermPlanner.Label(k))));
}