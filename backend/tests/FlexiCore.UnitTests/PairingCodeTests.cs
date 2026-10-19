using FlexiCore.Devices.Security;

namespace FlexiCore.UnitTests;

public sealed class PairingCodeTests
{
    [Fact]
    public void Generated_codes_are_ten_characters_without_look_alikes_and_vary()
    {
        var seen = new HashSet<string>();
        for (var i = 0; i < 500; i++)
        {
            var code = PairingCode.Generate();
            Assert.Equal(PairingCode.Length, code.Length);
            Assert.All(code, c => Assert.True(char.IsAsciiLetterUpper(c) || (c >= '2' && c <= '9')));
            Assert.DoesNotContain('O', code);
            Assert.DoesNotContain('I', code);
            seen.Add(code);
        }

        Assert.True(seen.Count > 490);
    }

    [Fact]
    public void Format_groups_five_and_five()
    {
        Assert.Equal("ABCDE-FGHJK", PairingCode.Format("ABCDEFGHJK"));
    }

    [Theory]
    [InlineData("ABCDEFGHJK")]
    [InlineData("abcde-fghjk")]
    [InlineData("  ABCDE FGHJK  ")]
    public void Normalize_accepts_case_dash_and_space_variants(string input)
    {
        Assert.True(PairingCode.TryNormalize(input, out var normalized));
        Assert.Equal("ABCDEFGHJK", normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABCDEFGHJ")]
    [InlineData("ABCDEFGHJKL")]
    [InlineData("ABCDEFGHJ0")]
    [InlineData("ABCDEFGHJ1")]
    [InlineData("ABCDEFGHJO")]
    [InlineData("ABCDEFGHJI")]
    public void Normalize_rejects_wrong_length_and_look_alike_characters(string? input)
    {
        Assert.False(PairingCode.TryNormalize(input, out _));
    }

    [Fact]
    public void Normalize_rejects_oversized_input()
    {
        Assert.False(PairingCode.TryNormalize(new string('A', 500), out _));
    }

    [Fact]
    public void Hash_is_32_bytes_deterministic_and_code_specific()
    {
        Assert.Equal(32, PairingCode.Hash("ABCDEFGHJK").Length);
        Assert.Equal(PairingCode.Hash("ABCDEFGHJK"), PairingCode.Hash("ABCDEFGHJK"));
        Assert.NotEqual(PairingCode.Hash("ABCDEFGHJK"), PairingCode.Hash("ABCDEFGHJL"));
    }
}