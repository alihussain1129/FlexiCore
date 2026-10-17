namespace FlexiCore.Licensing;

public enum LicenseState
{
    NotApplicable,
    Missing,
    Valid,
    ValidInGrace,
    Expired,
    NotYetValid,
    /// <summary>A file is stored (or was submitted) but failed verification.</summary>
    Invalid,
    /// <summary>The system cannot decide: no trusted keys, or the store cannot be read. Never means "accepted".</summary>
    Unavailable,
}

/// <summary>Current license state. License details are present only for authentic files (including expired and not-yet-valid).</summary>
public sealed record LicenseStatusReport(
    LicenseState State,
    string? Reason,
    LicensePayload? License,
    DateTimeOffset? GraceEndsAt,
    DateTimeOffset CheckedAt);

public enum LicenseImportOutcome
{
    Imported,
    Unchanged,
    Rejected,
    OlderThanCurrent,
    VerificationUnavailable,
    StorageFailed,
}

public sealed record LicenseImportResult(
    LicenseImportOutcome Outcome,
    LicenseStatus Verification,
    LicenseStatusReport? Report);

/// <summary>
/// Reads and imports licenses through an <see cref="ILicenseStore"/>. A license is fully verified before the store is
/// touched, so a failed import never changes the stored file. This service enforces nothing; it only reports.
/// </summary>
public sealed class LicenseService
{
    private readonly ILicenseStore _store;
    private readonly LicenseVerifier _verifier;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _importLock = new(1, 1);
    private readonly IInstallationIdentity _identity;
    private readonly bool _requireBinding;

    public LicenseService(
        ILicenseStore store, LicenseVerifier verifier, TimeProvider time,
        IInstallationIdentity? identity = null, bool requireBinding = false)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(time);
        _store = store;
        _verifier = verifier;
        _time = time;
        _identity = identity ?? new MachineInstallationIdentity();
        _requireBinding = requireBinding;
    }

    public async Task<LicenseStatusReport> GetStatusAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        if (_verifier.TrustedKeyCount == 0)
            return new LicenseStatusReport(LicenseState.Unavailable, "no_trusted_keys", null, null, now);

        byte[]? stored;
        try
        {
            stored = await _store.ReadAsync(cancellationToken);
        }
        catch (LicenseStorageException)
        {
            return new LicenseStatusReport(LicenseState.Unavailable, "storage_error", null, null, now);
        }

        if (stored is null)
            return new LicenseStatusReport(LicenseState.Missing, null, null, null, now);

        return Evaluate(Gate(_verifier.Verify(stored)), now);
    }

    public async Task<LicenseImportResult> ImportAsync(byte[] content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length > LicenseLimits.MaxFileBytes)
            return new LicenseImportResult(LicenseImportOutcome.Rejected, LicenseStatus.FileTooLarge, null);

        if (ActivationCode.LooksLikeCode(content))
        {
            if (!ActivationCode.TryDecode(content, out var decoded))
                return new LicenseImportResult(LicenseImportOutcome.Rejected, LicenseStatus.Malformed, null);
            content = decoded;
        }

        var candidate = Gate(_verifier.Verify(content));
        if (candidate.Status == LicenseStatus.NoTrustedKeys)
            return new LicenseImportResult(LicenseImportOutcome.VerificationUnavailable, candidate.Status, null);
        if (!candidate.IsAccepted)
            return new LicenseImportResult(LicenseImportOutcome.Rejected, candidate.Status, null);

        await _importLock.WaitAsync(cancellationToken);
        try
        {
            byte[]? current;
            try
            {
                current = await _store.ReadAsync(cancellationToken);
            }
            catch (LicenseStorageException)
            {
                return new LicenseImportResult(LicenseImportOutcome.StorageFailed, candidate.Status, null);
            }

            if (current is not null)
            {
                if (current.AsSpan().SequenceEqual(content.AsSpan()))
                    return new LicenseImportResult(
                        LicenseImportOutcome.Unchanged, candidate.Status, Evaluate(candidate, _time.GetUtcNow()));

                // Refuse to roll back to an older license. A current file that is not authentic has no payload and never blocks.
                var existing = _verifier.Verify(current).Payload;
                if (existing is not null && candidate.Payload!.IssuedAt < existing.IssuedAt)
                    return new LicenseImportResult(LicenseImportOutcome.OlderThanCurrent, candidate.Status, null);
            }

            try
            {
                await _store.WriteAsync(content, cancellationToken);
            }
            catch (LicenseStorageException)
            {
                return new LicenseImportResult(LicenseImportOutcome.StorageFailed, candidate.Status, null);
            }

            return new LicenseImportResult(
                LicenseImportOutcome.Imported, candidate.Status, Evaluate(candidate, _time.GetUtcNow()));
        }
        finally
        {
            _importLock.Release();
        }
    }

    /// <summary>
    /// Installation binding. An accepted license bound to another computer is refused; an unbound license is
    /// refused when binding is required. Fail closed: if this computer's identity cannot be read, bound licenses fail.
    /// </summary>
    private LicenseVerificationResult Gate(LicenseVerificationResult r)
    {
        if (!r.IsAccepted || r.Payload is null) return r;
        var bound = r.Payload.InstallationId;
        if (bound is null)
            return _requireBinding ? new LicenseVerificationResult(LicenseStatus.NotBound, null) : r;

        var local = _identity.InstallationId;
        if (local is null || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(bound), System.Text.Encoding.ASCII.GetBytes(local)))
            return new LicenseVerificationResult(LicenseStatus.WrongInstallation, null);
        return r;
    }

    /// <summary>Machine-readable reason for a verification status.</summary>
    public static string? ReasonCode(LicenseStatus status) => status switch
    {
        LicenseStatus.Valid or LicenseStatus.ValidInGrace => null,
        LicenseStatus.FileTooLarge => "file_too_large",
        LicenseStatus.NoTrustedKeys => "no_trusted_keys",
        LicenseStatus.Malformed => "malformed",
        LicenseStatus.UnsupportedSchemaVersion => "unsupported_schema_version",
        LicenseStatus.UnknownKeyId => "unknown_key_id",
        LicenseStatus.InvalidSignature => "invalid_signature",
        LicenseStatus.InvalidPayload => "invalid_payload",
        LicenseStatus.WrongProduct => "wrong_product",
        LicenseStatus.NotYetValid => "not_yet_valid",
        LicenseStatus.Expired => "expired",
        LicenseStatus.WrongInstallation => "wrong_installation",
        LicenseStatus.NotBound => "not_bound",
        _ => "unknown",
    };

    private static LicenseStatusReport Evaluate(LicenseVerificationResult result, DateTimeOffset now)
    {
        var payload = result.Payload;
        return result.Status switch
        {
            LicenseStatus.Valid =>
                new LicenseStatusReport(LicenseState.Valid, null, payload, GraceEnds(payload), now),
            LicenseStatus.ValidInGrace =>
                new LicenseStatusReport(LicenseState.ValidInGrace, null, payload, GraceEnds(payload), now),
            LicenseStatus.Expired =>
                new LicenseStatusReport(LicenseState.Expired, "expired", payload, GraceEnds(payload), now),
            LicenseStatus.NotYetValid =>
                new LicenseStatusReport(LicenseState.NotYetValid, "not_yet_valid", payload, null, now),
            LicenseStatus.NoTrustedKeys =>
                new LicenseStatusReport(LicenseState.Unavailable, "no_trusted_keys", null, null, now),
            _ =>
                new LicenseStatusReport(LicenseState.Invalid, ReasonCode(result.Status), null, null, now),
        };
    }

    private static DateTimeOffset? GraceEnds(LicensePayload? payload) =>
        payload is { ExpiresAt: { } expires, GraceDays: > 0 }
            ? LicenseVerifier.GraceEnd(expires, payload.GraceDays)
            : null;
}