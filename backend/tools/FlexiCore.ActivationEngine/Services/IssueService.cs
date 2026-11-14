using System.Security.Cryptography;
using System.Text;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Services;

public sealed record IssueInput(
    string KeyId,
    string Passphrase,
    string LicenseId,
    string Licensee,
    DateTimeOffset NotBefore,
    DateTimeOffset? ExpiresAt,
    int GraceDays,
    int MaxInstallations,
    string? InstallationId,
    string OutputPath,
    bool WriteActivationCode);

public sealed record IssueOutcome(
    string LicenseId,
    string Licensee,
    string KeyId,
    string LicensePath,
    string? ActivationCodePath,
    DateTimeOffset NotBefore,
    DateTimeOffset? ExpiresAt,
    bool Bound,
    string VerifiedWith,
    string Sha256,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Orchestrates one issuance using the existing LicenseIssuer, KeyStore, PublicKeyExport and IssueLedger.
/// Order: validate, refuse duplicates, sign (built-in self-check), verify against the exported public key,
/// write files without overwriting, then record in the ledger. A ledger failure removes the files again.
/// </summary>
public sealed class IssueService
{
    private readonly OwnerPaths _paths;
    private readonly TimeProvider _time;
    private readonly KeyService _keys;
    private readonly KeyManagementService _keyManagement;

    public IssueService(
        OwnerPaths paths,
        TimeProvider time,
        KeyService keys,
        KeyManagementService keyManagement)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        _keyManagement = keyManagement ?? throw new ArgumentNullException(nameof(keyManagement));
    }

    public static LicenseRequest BuildRequest(IssueInput input, DateTimeOffset now) =>
        LicenseIssuer.Validate(
            new LicenseRequest(
                LicenseId: input.LicenseId.Trim(),
                Licensee: input.Licensee.Trim(),
                IssuedAt: now,
                NotBefore: input.NotBefore,
                ExpiresAt: input.ExpiresAt,
                GraceDays: input.GraceDays,
                MaxInstallations: input.MaxInstallations,
                InstallationId: input.InstallationId),
            allowOtherProduct: false);

    public IssueOutcome Issue(IssueInput input)
    {
        var now = _time.GetUtcNow();
        KeyStore.ValidateKeyId(input.KeyId);
        var request = BuildRequest(input, now);

        // Enforce owner key policy here, not only in the UI.
        // A damaged key list or registry must never silently make a key eligible.
        var keyOverview = _keyManagement.Load();
        if (keyOverview.Problem is not null)
            throw new LicenseToolException(
                "The signing-key status could not be verified, so nothing was issued. "
                + keyOverview.Problem, 2);

        if (!keyOverview.IsSelectable(input.KeyId))
            throw new LicenseToolException(
                "Signing key '" + input.KeyId
                + "' is not eligible for issuance. Check its status and ERP trust registration.");

        // Output location: never overwrite, folder must exist.
        OutputPathGuard.EnsureAbsolute(input.OutputPath, "The license file path");
        var licensePath = Path.GetFullPath(input.OutputPath);
        var codePath = input.WriteActivationCode ? licensePath + ".code.txt" : null;
        var directory = Path.GetDirectoryName(licensePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            throw new LicenseToolException("The destination folder does not exist.");
        if (File.Exists(licensePath) || (codePath is not null && File.Exists(codePath)))
            throw new LicenseToolException("A file already exists at that location. Nothing was overwritten. Choose another name.");

        // The ledger must be usable before anything is signed.
        OutputPathGuard.EnsureOutsideGitWorkingTree(_paths.Root, "The owner data folder");
        try { Directory.CreateDirectory(_paths.Root); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LicenseToolException("The owner data folder cannot be created (" + ex.GetType().Name + "). Nothing was issued.", 2);
        }

        // Keep duplicate detection, signing, output creation and ledger append in one transaction.
        using var ledgerLock = LedgerFileLock.Acquire(_paths.LedgerPath);
        // Strict capture, the same code path restore uses: a ledger that is not valid UTF-8, cannot be read or is
        // oversized fails closed. The lenient reader would turn a bad byte into a replacement character and let a
        // damaged record pass as readable.
        var snapshot = LedgerReader.CaptureLive(_paths.LedgerPath, ledgerLock);
        if (snapshot.Problem is not null)
            throw new LicenseToolException("The issue ledger is not usable, so nothing was issued. " + snapshot.Problem, 2);
        if (snapshot.UnreadableLines > 0)
            throw new LicenseToolException(
                "The issue ledger has " + snapshot.UnreadableLines
                + " line(s) that cannot be read, so the duplicate check cannot be trusted and nothing was issued. "
                + "Repair or restore the ledger first.", 2);
        if (snapshot.Entries.Any(e => string.Equals(e.LicenseId, request.LicenseId, StringComparison.Ordinal)))
            throw new LicenseToolException("The license id '" + request.LicenseId + "' was already issued. Use a new id.");

        // Sign with the existing issuer. It verifies the result with the production verifier before returning.
        using var signer = _keys.OpenSigner(input.KeyId, input.Passphrase);
        var file = LicenseIssuer.Issue(request, signer);

        // Independently verify against the exported public key that is distributed to customers.
        // Never fall back to the signer's own public key: that would not establish that the
        // production/customer trust material matches the private signing key.
        var publicPath = Path.Combine(_paths.KeysDirectory, input.KeyId + ".public.json");
        if (!File.Exists(publicPath))
            throw new LicenseToolException(
                "The exported public key file is missing for '" + input.KeyId
                + "'. Nothing was written. Restore or export the matching public key before issuing.");

        var trusted = PublicKeyExport.Read(publicPath);
        var matchingKeys = trusted
            .Where(key => string.Equals(key.KeyId, signer.KeyId, StringComparison.Ordinal))
            .ToArray();

        if (matchingKeys.Length != 1)
            throw new LicenseToolException(
                "The exported public key set must contain exactly one entry for '" + signer.KeyId
                + "'. Nothing was written.");

        if (!matchingKeys[0].SubjectPublicKeyInfo.AsSpan()
                .SequenceEqual(signer.SubjectPublicKeyInfo))
            throw new LicenseToolException(
                "The exported public key does not match the signing key '" + signer.KeyId
                + "'. Nothing was written.");

        const string verifiedWithPrefix = "exported public key file ";
        var verifiedWith = verifiedWithPrefix + input.KeyId + ".public.json";

        var check = new LicenseVerifier(trusted, new PinnedTimeProvider(request.NotBefore)).Verify(file);
        if (check.Status != LicenseStatus.Valid)
            throw new LicenseToolException(
                "Verification against the public key failed (" + check.Status + "). Nothing was written. "
                + "The public key file may not match this private key.", 2);

        var warnings = new List<string>();
        if (OutputPathGuard.IsInsideGitWorkingTree(directory))
            warnings.Add("The license file is inside a git working tree. It names a customer; do not commit it.");

        var written = new List<string>();
        try
        {
            SafeFile.WriteNew(licensePath, file);
            written.Add(licensePath);

            if (codePath is not null)
            {
                SafeFile.WriteNew(codePath, Encoding.ASCII.GetBytes(ActivationCode.Encode(file)));
                written.Add(codePath);
            }

            try
            {
                IssueLedger.Append(_paths.LedgerPath, request, signer.KeyId, file, now, ledgerLock);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new LicenseToolException(
                    "The issue ledger could not be written (" + ex.GetType().Name + "), so the license was NOT issued. No license file was kept.", 2);
            }
        }
        catch
        {
            var leftovers = new List<string>();
            foreach (var path in written)
            {
                try { File.Delete(path); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { leftovers.Add(path); }
            }
            if (leftovers.Count > 0)
                throw new LicenseToolException(
                    "Issuing failed after a file was written that could not be removed. Delete it manually; it was NOT recorded in the ledger: "
                    + string.Join("; ", leftovers), 2);
            throw;
        }

        return new IssueOutcome(
            request.LicenseId, request.Licensee, signer.KeyId, licensePath, codePath,
            request.NotBefore, request.ExpiresAt, request.InstallationId is not null,
            verifiedWith, Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant(), warnings);
    }
}
