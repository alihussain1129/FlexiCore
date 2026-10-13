using FlexiCore.Identity.Settings;
using Microsoft.Extensions.Configuration;

namespace FlexiCore.IntegrationTests;

public sealed class StartupSecurityTests
{
    [Fact]
    public void Jwt_settings_reject_a_missing_signing_key()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = ""
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(
            () => JwtSettings.Load(configuration));

        Assert.Contains("Jwt:SigningKey is not configured", ex.Message);
    }
}
