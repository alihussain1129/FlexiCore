namespace FlexiCore.Licensing;

/// <summary>Strict limits applied to every license file before and during parsing.</summary>
public static class LicenseLimits
{
    public const int SupportedSchemaVersion = 1;
    public const string ExpectedProduct = "FlexiCore";

    public const int MaxFileBytes = 16 * 1024;
    public const int MaxPayloadBytes = 4096;
    /// <summary>Base64url length of a 4096-byte payload, rounded up.</summary>
    public const int MaxPayloadBase64Chars = 5464;
    public const int MaxSignatureBase64Chars = 128;
    /// <summary>ES256 signature: 32-byte r followed by 32-byte s (IEEE P1363).</summary>
    public const int SignatureBytes = 64;
    public const int MaxJsonDepth = 8;

    public const int MaxKeyIdLength = 64;
    public const int MaxLicenseIdLength = 64;
    public const int MaxProductLength = 64;
    public const int MaxLicenseeLength = 200;
    public const int MaxGraceDays = 365;
    public const int MaxInstallationsLimit = 100_000;

    public const int MaxTrustedKeys = 8;
    public const int MaxKeySetBytes = 64 * 1024;
}