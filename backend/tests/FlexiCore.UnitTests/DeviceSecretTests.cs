using FlexiCore.Devices.Security;

namespace FlexiCore.UnitTests;

public sealed class DeviceSecretTests
{
    private const string ValidId = "0123456789abcdef0123456789abcdef";

    private static bool TryParse(string? header) => DeviceSecret.TryParseAuthorizationHeader(header, out _, out _);

    [Fact]
    public void Generated_secrets_are_43_url_safe_characters_and_unique()
    {
        var seen = new HashSet<string>();
        for (var i = 0; i < 200; i++)
        {
            var secret = DeviceSecret.Generate();
            Assert.Equal(DeviceSecret.SecretTextLength, secret.Length);
            Assert.All(secret, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
            Assert.True(seen.Add(secret));
        }
    }

    [Fact]
    public void Hash_is_32_bytes_deterministic_and_specific_to_the_secret()
    {
        var a = DeviceSecret.Generate();
        var b = DeviceSecret.Generate();

        Assert.Equal(32, DeviceSecret.Hash(a).Length);
        Assert.Equal(DeviceSecret.Hash(a), DeviceSecret.Hash(a));
        Assert.NotEqual(DeviceSecret.Hash(a), DeviceSecret.Hash(b));
        Assert.DoesNotContain(a, Convert.ToBase64String(DeviceSecret.Hash(a)));
    }

    [Fact]
    public void Verify_accepts_only_the_matching_secret()
    {
        var secret = DeviceSecret.Generate();
        var hash = DeviceSecret.Hash(secret);

        Assert.True(DeviceSecret.Verify(secret, hash));
        Assert.False(DeviceSecret.Verify(DeviceSecret.Generate(), hash));
        Assert.False(DeviceSecret.Verify(secret, new byte[16]));
        Assert.False(DeviceSecret.Verify(secret, new byte[32]));
    }

    [Fact]
    public void Credential_header_round_trips()
    {
        var id = Guid.NewGuid();
        var secret = DeviceSecret.Generate();
        var header = "Device " + DeviceSecret.FormatCredential(id, secret);

        Assert.True(DeviceSecret.TryParseAuthorizationHeader(header, out var parsedId, out var parsedSecret));
        Assert.Equal(id, parsedId);
        Assert.Equal(secret, parsedSecret);
    }

    [Fact]
    public void Scheme_is_case_insensitive_and_bearer_is_not_a_device_header()
    {
        var secret = new string('A', 43);

        Assert.True(TryParse($"device {ValidId}.{secret}"));
        Assert.True(DeviceSecret.IsDeviceHeader("DEVICE x"));
        Assert.False(TryParse($"Bearer {ValidId}.{secret}"));
        Assert.False(DeviceSecret.IsDeviceHeader("Bearer abc"));
    }

    [Fact]
    public void Malformed_credentials_are_rejected()
    {
        var secret = new string('A', 43);

        Assert.False(TryParse(null));
        Assert.False(TryParse(""));
        Assert.False(TryParse("Device"));
        Assert.False(TryParse("Device "));
        Assert.False(TryParse($"Device {ValidId}{secret}"));
        Assert.False(TryParse($"Device {ValidId}.{secret}x"));
        Assert.False(TryParse($"Device {ValidId}.{new string('A', 42)}"));
        Assert.False(TryParse($"Device {ValidId}.{new string('A', 42)}+"));
        Assert.False(TryParse($"Device {new string('g', 32)}.{secret}"));
        Assert.False(TryParse($"Device {new string('0', 32)}.{secret}"));
        Assert.False(TryParse("Device " + new string('A', 300)));
    }
}