namespace FlexiCore.Licensing;

/// <summary>Signed license contents. MaxInstallations is informational only; nothing enforces it in this slice.</summary>
public sealed record LicensePayload(
    string LicenseId,
    string Product,
    string Licensee,
    DateTimeOffset IssuedAt,
    DateTimeOffset NotBefore,
    DateTimeOffset? ExpiresAt,
    int GraceDays,
    int MaxInstallations,
    string? InstallationId = null);