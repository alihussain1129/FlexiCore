using FlexiCore.Identity.Security;

namespace FlexiCore.UnitTests;

public class PasswordPolicyTests
{
    private const string Email = "alice.smith@example.com";

    [Fact] public void Eleven_characters_are_rejected() => Assert.NotEmpty(PasswordPolicy.Validate(new string('x', 11), Email));
    [Fact] public void Twelve_characters_are_accepted() => Assert.Empty(PasswordPolicy.Validate(new string('x', 12), Email));
    [Fact] public void One_hundred_twenty_eight_characters_are_accepted() => Assert.Empty(PasswordPolicy.Validate(new string('x', 128), Email));
    [Fact] public void One_hundred_twenty_nine_characters_are_rejected() => Assert.NotEmpty(PasswordPolicy.Validate(new string('x', 129), Email));

    [Fact]
    public void Null_and_empty_are_rejected()
    {
        Assert.NotEmpty(PasswordPolicy.Validate(null, Email));
        Assert.NotEmpty(PasswordPolicy.Validate("", Email));
    }

    [Fact]
    public void A_password_containing_the_email_local_part_is_rejected()
    {
        var errors = PasswordPolicy.Validate("zz-alice.smith-zz", Email);
        Assert.Contains(errors, e => e.Contains("email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_local_part_check_ignores_case() =>
        Assert.NotEmpty(PasswordPolicy.Validate("ZZ-ALICE.SMITH-ZZ", Email));

    [Fact]
    public void An_unrelated_long_password_is_accepted() =>
        Assert.Empty(PasswordPolicy.Validate("correct-horse-battery", Email));

    [Fact]
    public void A_local_part_shorter_than_four_characters_is_not_enforced() =>
        Assert.Empty(PasswordPolicy.Validate("alphabet-soup-12", "al@example.com"));

    [Fact]
    public void A_local_part_of_exactly_four_characters_is_enforced() =>
        Assert.NotEmpty(PasswordPolicy.Validate("xxjohnxxxxxx", "john@example.com"));

    [Fact]
    public void Length_counts_unicode_characters_not_utf16_units()
    {
        var twelve = string.Concat(Enumerable.Repeat("\U0001F600", 12));
        var eleven = string.Concat(Enumerable.Repeat("\U0001F600", 11));
        Assert.Empty(PasswordPolicy.Validate(twelve, Email));
        Assert.NotEmpty(PasswordPolicy.Validate(eleven, Email));
    }
}

public class EmailRulesTests
{
    [Theory]
    [InlineData("a@b.co")]
    [InlineData("first.last+tag@sub.example.com")]
    [InlineData("  Ali@Example.COM  ")]
    public void Valid_addresses_are_accepted(string email) => Assert.True(EmailRules.IsValid(email));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plainaddress")]
    [InlineData("@example.com")]
    [InlineData("a@")]
    [InlineData("a@b")]
    [InlineData("a@@b.co")]
    [InlineData("a b@example.com")]
    [InlineData(".a@b.co")]
    [InlineData("a..b@b.co")]
    [InlineData("a@-b.co")]
    [InlineData("a@b..co")]
    public void Invalid_addresses_are_rejected(string? email) => Assert.False(EmailRules.IsValid(email));

    [Fact]
    public void A_local_part_over_64_characters_is_rejected() =>
        Assert.False(EmailRules.IsValid(new string('a', 65) + "@b.co"));

    [Fact]
    public void An_address_over_254_characters_is_rejected() =>
        Assert.False(EmailRules.IsValid("a@" + string.Join(".", Enumerable.Repeat(new string('b', 60), 5)) + ".co"));

    [Fact]
    public void Normalize_trims_and_lowercases() =>
        Assert.Equal("ali@example.com", EmailRules.Normalize("  Ali@Example.COM "));
}