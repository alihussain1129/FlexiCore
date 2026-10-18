using FlexiCore.Identity.Security;
using Xunit;

namespace FlexiCore.UnitTests;

public class UsernameRulesTests
{
    [Theory]
    [InlineData("ali")]
    [InlineData("Ali.Hussain")]
    [InlineData("a_b-c9")]
    [InlineData("OWNER01")]
    public void Valid_usernames_are_accepted(string value) => Assert.True(UsernameRules.IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("-ali")]
    [InlineData("a li")]
    [InlineData("ali!")]
    [InlineData("ali@example.com")]
    public void Invalid_usernames_are_rejected(string value) => Assert.False(UsernameRules.IsValid(value));

    [Fact]
    public void A_thirty_three_character_username_is_rejected() =>
        Assert.False(UsernameRules.IsValid(new string('a', 33)));

    [Fact]
    public void A_thirty_two_character_username_is_accepted() =>
        Assert.True(UsernameRules.IsValid(new string('a', 32)));

    [Fact]
    public void Normalization_trims_and_lowercases() =>
        Assert.Equal("ali.hussain", UsernameRules.Normalize("  Ali.HUSSAIN "));

    [Fact]
    public void Null_is_rejected() => Assert.False(UsernameRules.IsValid(null));
}