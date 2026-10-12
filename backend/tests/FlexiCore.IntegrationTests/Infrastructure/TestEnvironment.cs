using System.Runtime.CompilerServices;
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

internal static class TestEnvironment
{
    // The API now requires Jwt:SigningKey at startup. Supplying a test key here keeps the Phase 1a
    // health tests working without editing them.
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Initialize() =>
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestSecrets.SigningKeyBase64);
#pragma warning restore CA2255
}