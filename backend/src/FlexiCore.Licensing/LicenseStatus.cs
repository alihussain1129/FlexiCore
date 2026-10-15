namespace FlexiCore.Licensing;

/// <summary>
/// Deterministic outcome of verifying a license file. Checks run in a fixed order:
/// FileTooLarge, NoTrustedKeys, Malformed, UnsupportedSchemaVersion, UnknownKeyId,
/// InvalidSignature, InvalidPayload, WrongProduct, NotYetValid, then Valid / ValidInGrace / Expired.
/// </summary>
public enum LicenseStatus
{
    Valid,
    ValidInGrace,
    FileTooLarge,
    NoTrustedKeys,
    Malformed,
    UnsupportedSchemaVersion,
    UnknownKeyId,
    InvalidSignature,
    InvalidPayload,
    WrongProduct,
    NotYetValid,
    Expired,
}

/// <summary>
/// Result of verification. <see cref="Payload"/> is set only when the license is accepted.
/// Note: a valid result proves the file was signed by a trusted key. It does not make the
/// license tamper-proof against someone patching the client, and it does not bind to a device.
/// </summary>
public sealed record LicenseVerificationResult(LicenseStatus Status, LicensePayload? Payload)
{
    public bool IsAccepted => Status is LicenseStatus.Valid or LicenseStatus.ValidInGrace;
}