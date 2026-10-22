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

    public IssueService(OwnerPaths paths, TimeProvider time)
    {
        _paths = paths;
        _time = time;
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

        var snapshot = LedgerReader.Read(_paths.LedgerPath);
        if (snapshot.Problem is not null)
            throw new LicenseToolException("The issue ledger is not usable, so nothing was issued. " + snapshot.Problem, 2);
        if (snapshot.Entries.Any(e => string.Equals(e.LicenseId, request.LicenseId, StringComparison.Ordinal)))
            throw new LicenseToolException("The license id '" + request.LicenseId + "' was already issued. Use a new id.");

        // Sign with the existing issuer. It verifies the result with the production verifier before returning.
        var privatePath = Path.Combine(_paths.KeysDirectory, input.KeyId + ".p8");
        using var signer = KeyStore.Load(privatePath, input.Passphrase);
        var file = LicenseIssuer.Issue(request, signer);

        // Second, independent check against the public key file that gets shipped to customers.
        var publicPath = Path.Combine(_paths.KeysDirectory, input.KeyId + ".public.json");
        IReadOnlyList<TrustedKey> trusted;
        string verifiedWith;
        if (File.Exists(publicPath))
        {
            trusted = PublicKeyExport.Read(publicPath);
            verifiedWith = "exported public key file " + input.KeyId + ".public.json";
        }
        else
        {
            trusted = new[] { new TrustedKey(signer.KeyId, signer.SubjectPublicKeyInfo) };
            verifiedWith = "the signing key's own public key (no public key file found)";
        }

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
                IssueLedger.Append(_paths.LedgerPath, request, signer.KeyId, file, now);
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
