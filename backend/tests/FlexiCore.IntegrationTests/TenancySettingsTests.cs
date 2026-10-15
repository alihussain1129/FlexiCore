using Microsoft.Extensions.Configuration;
using FlexiCore.Tenancy;

namespace FlexiCore.IntegrationTests;

// Needs no database.
public sealed class TenancySettingsTests
{
    private static IConfiguration Config(string? value)
    {
        var values = new Dictionary<string, string?>();
        if (value is not null) values["Tenancy:RateLimit:CreatePermitPerMinute"] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void The_default_is_ten_creations_per_minute()
    {
        Assert.Equal(10, TenancySettings.From(Config(null)).CreatePermitPerMinute);
    }

    [Fact]
    public void A_configured_value_is_used()
    {
        Assert.Equal(25, TenancySettings.From(Config("25")).CreatePermitPerMinute);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1000001")]
    [InlineData("abc")]
    public void Invalid_values_are_rejected(string value)
    {
        Assert.Throws<InvalidOperationException>(() => TenancySettings.From(Config(value)));
    }
}