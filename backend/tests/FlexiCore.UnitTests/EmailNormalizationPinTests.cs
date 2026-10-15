using FlexiCore.Identity.Security;
using FlexiCore.Tenancy;

namespace FlexiCore.UnitTests;

public class EmailNormalizationPinTests
{
    [Theory]
    [InlineData("Ali@Example.COM")]
    [InlineData("  ali@example.com  ")]
    [InlineData("\tALI@EXAMPLE.COM\r\n")]
    [InlineData("first.last+Tag@Sub.Example.Com")]
    [InlineData("\u00C4LI@EXAMPLE.COM")]
    [InlineData("\u0130@example.com")]
    [InlineData("\u01C5@example.com")]
    [InlineData("already@lower.case")]
    public void Tenancy_and_Identity_normalize_every_input_identically(string email) =>
        Assert.Equal(EmailRules.Normalize(email), MemberEmail.Normalize(email));

    [Fact]
    public void A_missing_email_stays_missing() => Assert.Null(MemberEmail.Normalize(null));
}