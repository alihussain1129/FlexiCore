using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;

namespace FlexiCore.Identity.Security;

public enum PasswordCheck
{
    Failed,
    Success,
    SuccessRehashNeeded
}

public interface IPasswordService
{
    string Hash(string password);
    PasswordCheck Verify(string storedHash, string password);

    /// <summary>Spends the same effort as a real verification, so unknown accounts are not distinguishable by timing.</summary>
    void BurnVerificationTime(string password);
}

/// <summary>Thin wrapper over the framework PasswordHasher (PBKDF2). No custom cryptography.</summary>
public sealed class PasswordService : IPasswordService
{
    private sealed class HashSubject;

    private readonly PasswordHasher<HashSubject> _hasher;
    private readonly HashSubject _subject = new();
    private readonly string _dummyHash;

    public PasswordService(int iterations)
    {
        _hasher = new PasswordHasher<HashSubject>(Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = iterations
        }));
        _dummyHash = _hasher.HashPassword(_subject, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public string Hash(string password) => _hasher.HashPassword(_subject, password);

    public PasswordCheck Verify(string storedHash, string password) =>
        _hasher.VerifyHashedPassword(_subject, storedHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
            _ => PasswordCheck.Failed
        };

    public void BurnVerificationTime(string password) =>
        _ = _hasher.VerifyHashedPassword(_subject, _dummyHash, password);
}