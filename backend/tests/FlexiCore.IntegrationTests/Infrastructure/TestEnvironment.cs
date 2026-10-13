using System.Security.Cryptography;
using System.Text;

namespace FlexiCore.IntegrationTests.Infrastructure;

public static class TestSecrets
{
    // Test-only key derived at runtime; it exists only inside the test process.
    public static readonly byte[] SigningKey =
        SHA256.HashData(Encoding.UTF8.GetBytes("flexicore-integration-tests-signing-key"));

    public static string SigningKeyBase64 => Convert.ToBase64String(SigningKey);
}
