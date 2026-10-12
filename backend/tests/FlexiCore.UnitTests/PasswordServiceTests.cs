using System.Buffers.Binary;
using FlexiCore.Identity.Security;
using FlexiCore.Identity.Settings;

namespace FlexiCore.UnitTests;

public class PasswordServiceTests
{
    private const string Password = "correct-horse-battery-staple";

    private static PasswordService Service(int iterations = IdentitySettings.DefaultPbkdf2Iterations) => new(iterations);

    [Fact]
    public void Stored_hash_is_v3_format_with_hmac_sha512_and_the_configured_iterations()
    {
        var bytes = Convert.FromBase64String(Service().Hash(Password));

        Assert.Equal(0x01, bytes[0]);
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1, 4)));      // 2 = HMACSHA512
        Assert.Equal((uint)IdentitySettings.DefaultPbkdf2Iterations, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5, 4)));
        Assert.True(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(9, 4)) >= 16);      // salt length
    }

    [Fact]
    public void Hashes_never_contain_the_plaintext_and_are_salted()
    {
        var service = Service();
        var first = service.Hash(Password);
        var second = service.Hash(Password);

        Assert.DoesNotContain(Password, first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Verify_accepts_the_right_password_and_rejects_a_wrong_one()
    {
        var service = Service();
        var hash = service.Hash(Password);

        Assert.Equal(PasswordCheck.Success, service.Verify(hash, Password));
        Assert.Equal(PasswordCheck.Failed, service.Verify(hash, Password + "x"));
    }

    [Fact]
    public void A_hash_made_with_fewer_iterations_is_flagged_for_rehash()
    {
        var oldHash = Service(IdentitySettings.MinPbkdf2Iterations).Hash(Password);

        Assert.Equal(PasswordCheck.SuccessRehashNeeded, Service().Verify(oldHash, Password));
    }

    [Fact]
    public void A_current_hash_is_not_flagged_for_rehash()
    {
        var service = Service();
        Assert.Equal(PasswordCheck.Success, service.Verify(service.Hash(Password), Password));
    }

    [Fact]
    public void Burning_verification_time_does_not_throw() => Service().BurnVerificationTime(Password);
}