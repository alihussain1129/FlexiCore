using FlexiCore.Licensing;
using System.Security.Cryptography;
using System.Text;

namespace FlexiCore.LicenseTool;

/// <summary>
/// Exit codes: 0 success, 1 bad usage or input, 2 operation failed (wrong passphrase, self-check, I/O),
/// 3 verify ran but the license is not accepted.
/// </summary>
public static class LicenseToolApp
{
    public static int Run(string[] args, IToolConsole console, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(time);

        try
        {
            return Dispatch(args, console, time);
        }
        catch (LicenseToolException ex)
        {
            console.Error.WriteLine("Error: " + ex.Message);
            return ex.ExitCode;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            console.Error.WriteLine("Error: file access failed: " + ex.Message);
            return 2;
        }
    }

    private static int Dispatch(string[] args, IToolConsole console, TimeProvider time)
    {
        if (args.Length == 0)
        {
            PrintUsage(console.Error);
            return 1;
        }

        var rest = args[1..];
        switch (args[0])
        {
            case "keygen": return KeyGen(rest, console);
            case "provision-key": return ProvisionKey(rest, console);
            case "export-public": return ExportPublic(rest, console);
            case "build-keyset": return BuildKeySet(rest, console);
            case "revocation-snapshot": return RevocationSnapshot(rest, console, time);
            case "issue": return Issue(rest, console, time);
            case "renew": return Renew(rest, console, time);
            case "verify": return Verify(rest, console, time);
            case "make-test-vectors": return TestVectors.Run(rest, console);
            case "help":
            case "--help":
            case "-h":
                PrintUsage(console.Out);
                return 0;
            default:
                throw new LicenseToolException($"Unknown command '{args[0]}'. Run with 'help' for usage.");
        }
    }

    // ---- provision-key ----

    private static int ProvisionKey(string[] rest, IToolConsole console)
    {
        var p = ArgParser.Parse(rest, ["key", "public-keys", "out-dir"], []);
        var keys = PublicKeyExport.Read(Path.GetFullPath(p.Require("public-keys")));
        var provisioned = KeyStore.ProvisionAuthorized(
            p.Require("key"), p.Require("out-dir"), ReadPassphrase(console, "Signer passphrase: "), keys);
        console.Out.WriteLine($"Provisioned authorized signer '{provisioned.KeyId}' to {provisioned.PrivateKeyPath}");
        console.Out.WriteLine($"Fingerprint: {provisioned.Fingerprint}");
        console.Out.WriteLine("Trust set unchanged. Encrypted signer copied with owner-only filesystem permissions.");
        return 0;
    }

    // ---- keygen ----

    private static int KeyGen(string[] rest, IToolConsole console)
    {
        var p = ArgParser.Parse(rest, ["key-id", "out-dir"], ["force"]);
        var keyId = p.Require("key-id");
        var directory = p.Require("out-dir");
        KeyStore.ValidateKeyId(keyId);
        OutputPathGuard.EnsureOutsideGitWorkingTree(directory, "The key directory");
        if (OutputPathGuard.LooksCloudSynced(directory))
            console.Error.WriteLine("Warning: that folder looks cloud-synced. A private key file should not be synced.");

        var first = ReadPassphrase(console, "New passphrase: ");
        var problem = PassphrasePolicy.Validate(first);
        if (problem is not null) throw new LicenseToolException(problem);
        var second = ReadPassphrase(console, "Repeat passphrase: ");
        if (!string.Equals(first, second, StringComparison.Ordinal))
            throw new LicenseToolException("The passphrases did not match. Nothing was written.");

        var created = KeyStore.Create(keyId, directory, first, p.Flags.Contains("force"));

        console.Out.WriteLine($"Encrypted key file : {created.PrivateKeyPath}");
        console.Out.WriteLine($"Public key (safe)  : {created.PublicKeyPath}");
        console.Out.WriteLine($"Fingerprint        : {created.Fingerprint}");
        if (!created.AccessRestricted)
            console.Error.WriteLine("Warning: could not restrict the key file to the current user. Fix its permissions manually.");
        console.Out.WriteLine();
        console.Out.WriteLine("Now: make two encrypted backups on separate media, store the passphrase in a password manager,");
        console.Out.WriteLine("and keep a paper copy of it somewhere separate. See README.md (backup and recovery).");
        return 0;
    }

    // ---- export-public ----

    private static int ExportPublic(string[] rest, IToolConsole console)
    {
        var p = ArgParser.Parse(rest, ["key", "out"], ["force"]);
        var keyPath = p.Require("key");
        var outPath = p.Optional("out");

        using var signer = KeyStore.Load(keyPath, ReadPassphrase(console, "Passphrase: "));
        var bytes = PublicKeyExport.Build(new[] { new TrustedKey(signer.KeyId, signer.SubjectPublicKeyInfo) });

        if (outPath is null)
        {
            console.Out.WriteLine(System.Text.Encoding.UTF8.GetString(bytes));
        }
        else
        {
            WriteNew(Path.GetFullPath(outPath), bytes, p.Flags.Contains("force"), "The output file");
            console.Out.WriteLine($"Wrote public key set for '{signer.KeyId}' to {Path.GetFullPath(outPath)}");
        }
        console.Out.WriteLine($"Fingerprint: {KeyStore.Fingerprint(signer.SubjectPublicKeyInfo)}");
        return 0;
    }

    // ---- build-keyset ----

    private static int BuildKeySet(string[] rest, IToolConsole console)
    {
        var p = ArgParser.Parse(rest, ["out"], ["force"]);
        var outPath = Path.GetFullPath(p.Require("out"));
        if (p.Positionals.Count < 1)
            throw new LicenseToolException("build-keyset needs at least one public key file (the .public.json files).");

        var all = new List<TrustedKey>();
        foreach (var file in p.Positionals) all.AddRange(PublicKeyExport.Read(file));

        var duplicate = all.GroupBy(k => k.KeyId, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new LicenseToolException($"The key id '{duplicate.Key}' appears more than once.");

        var testKey = all.FirstOrDefault(k => k.KeyId.StartsWith("interop-test", StringComparison.Ordinal));
        if (testKey is not null)
            throw new LicenseToolException($"'{testKey.KeyId}' is a test key id and must never be in a production key set.");

        var bytes = PublicKeyExport.Build(all);
        WriteNew(outPath, bytes, p.Flags.Contains("force"), "The key set file");

        console.Out.WriteLine($"Wrote {all.Count} public key(s) to {outPath}");
        foreach (var key in all)
            console.Out.WriteLine($"  {key.KeyId}  {KeyStore.Fingerprint(key.SubjectPublicKeyInfo)}");
        return 0;
    }

    // ---- issue ----

    private static int Issue(string[] rest, IToolConsole console, TimeProvider time)
    {
        var p = ArgParser.Parse(
            rest,
            ["key", "public-keys", "license-id", "licensee", "issued-at", "not-before", "expires", "grace", "installations", "out", "ledger", "installation", "format"],
            ["force"]);

        var keyPath = p.Require("key");
        var publicKeysPath = Path.GetFullPath(p.Require("public-keys"));
        var trustedKeys = PublicKeyExport.Read(publicKeysPath);
        var outPath = Path.GetFullPath(p.Require("out"));
        var force = p.Flags.Contains("force");
        var format = p.Optional("format") ?? "file";
        if (format is not ("file" or "code" or "both"))
            throw new LicenseToolException("--format must be file, code or both.");
        if (!force && File.Exists(outPath))
            throw new LicenseToolException($"The license file already exists: {outPath}. Use --force to replace it.");

        var expiresText = p.Optional("expires")
                          ?? throw new LicenseToolException("Missing --expires. Give a date, or 'never' for a perpetual license.");
        var request = LicenseIssuer.Validate(
            new LicenseRequest(
                LicenseId: p.Require("license-id"),
                Licensee: p.Require("licensee"),
                IssuedAt: ParseTime("issued-at", p.Optional("issued-at") ?? "now", time),
                NotBefore: ParseTime("not-before", p.Optional("not-before") ?? "now", time),
                ExpiresAt: expiresText == "never" ? null : ParseTime("expires", expiresText, time),
                GraceDays: ParseInt("grace", p.Optional("grace"), 0),
                MaxInstallations: ParseInt("installations", p.Optional("installations"), 1),
                InstallationId: ParseInstallation(p.Optional("installation"))),
            allowOtherProduct: false);

        var ledger = p.Optional("ledger");
        if (ledger is not null)
        {
            OutputPathGuard.EnsureAbsolute(ledger, "The ledger path");
            OutputPathGuard.EnsureOutsideGitWorkingTree(Path.GetDirectoryName(Path.GetFullPath(ledger))!, "The ledger folder");
        }

        // If a ledger was requested, hold its lock until the record has been appended.
        using var ledgerLock = ledger is null ? null : LedgerFileLock.Acquire(ledger);

        if (OutputPathGuard.IsInsideGitWorkingTree(Path.GetDirectoryName(outPath)!))
            console.Error.WriteLine("Warning: the license file is inside a git working tree. It names a customer; do not commit it.");

        // Duplicate protection runs under the held lock, before the passphrase prompt. --force only controls
        // whether existing OUTPUT files may be replaced; it never bypasses the ledger check.
        if (ledger is not null) IssueLedger.EnsureNotIssued(ledger, request.LicenseId, ledgerLock!);

        using var signer = KeyStore.Load(keyPath, ReadPassphrase(console, "Passphrase: "));
        var matchingKeys = trustedKeys
            .Where(key => string.Equals(key.KeyId, signer.KeyId, StringComparison.Ordinal))
            .ToArray();
        if (matchingKeys.Length != 1
            || !matchingKeys[0].SubjectPublicKeyInfo.AsSpan().SequenceEqual(signer.SubjectPublicKeyInfo))
            throw new LicenseToolException(
                "The signing key does not exactly match one key in the supplied public key set. Nothing was issued.", 2);

        var file = LicenseIssuer.Issue(request, signer);
        var verification = new LicenseVerifier(trustedKeys, new PinnedTimeProvider(request.NotBefore)).Verify(file);
        if (verification.Status != LicenseStatus.Valid)
            throw new LicenseToolException(
                "The issued license did not verify with the supplied public key set (" + verification.Status + "). Nothing was written.", 2);

        var undo = new List<(string Path, byte[]? Original)>();
        try
        {
            WriteTracked(outPath, file, force, "The license file", undo);
            if (format is "code" or "both")
                WriteTracked(outPath + ".code.txt", System.Text.Encoding.ASCII.GetBytes(ActivationCode.Encode(file)), force, "The activation code file", undo);
            if (ledger is not null) IssueLedger.Append(ledger, request, signer.KeyId, file, time.GetUtcNow(), ledgerLock!);
        }
        catch
        {
            RollbackOutputs(undo, console);
            throw;
        }

        console.Out.WriteLine($"Issued {request.LicenseId} to '{request.Licensee}' with key '{signer.KeyId}'");
        console.Out.WriteLine($"  not before : {LicenseIssuer.FormatTimestamp(request.NotBefore)}");
        console.Out.WriteLine($"  expires    : {(request.ExpiresAt is { } e ? LicenseIssuer.FormatTimestamp(e) : "never")}");
        console.Out.WriteLine($"  file       : {outPath}");
        console.Out.WriteLine("  self-check : passed (license format and signature)");
        console.Out.WriteLine($"  trust check: passed (verified against {publicKeysPath})");
        return 0;
    }

    // ---- verify ----

    private static int Verify(string[] rest, IToolConsole console, TimeProvider time)
    {
        var p = ArgParser.Parse(rest, ["public-keys", "at"], []);
        if (p.Positionals.Count != 1) throw new LicenseToolException("verify needs exactly one license file.");
        var file = Path.GetFullPath(p.Positionals[0]);
        if (!File.Exists(file)) throw new LicenseToolException($"License file not found: {file}");

        var keys = PublicKeyExport.Read(p.Require("public-keys"));
        var at = p.Optional("at");
        TimeProvider clock = at is null ? time : new PinnedTimeProvider(ParseTime("at", at, time));

        var result = new LicenseVerifier(keys, clock).Verify(File.ReadAllBytes(file));

        console.Out.WriteLine($"Status: {result.Status}");
        if (result.Payload is { } payload)
        {
            console.Out.WriteLine($"  license id   : {payload.LicenseId}");
            console.Out.WriteLine($"  licensee     : {payload.Licensee}");
            console.Out.WriteLine($"  issued at    : {LicenseIssuer.FormatTimestamp(payload.IssuedAt)}");
            console.Out.WriteLine($"  not before   : {LicenseIssuer.FormatTimestamp(payload.NotBefore)}");
            console.Out.WriteLine($"  expires      : {(payload.ExpiresAt is { } e ? LicenseIssuer.FormatTimestamp(e) : "never")}");
            console.Out.WriteLine($"  grace days   : {payload.GraceDays}");
            console.Out.WriteLine($"  installations: {payload.MaxInstallations}");
        }
        return result.IsAccepted ? 0 : 3;
    }

    // ---- renew ----

    private static int Renew(string[] rest, IToolConsole console, TimeProvider time)
    {
        var p = ArgParser.Parse(rest,
            ["key", "public-keys", "license", "revocations", "ledger", "license-id", "expires", "out"], []);
        var publicKeysPath = Path.GetFullPath(p.Require("public-keys"));
        var trustedKeys = PublicKeyExport.Read(publicKeysPath);
        var ledgerPath = Path.GetFullPath(p.Require("ledger"));
        OutputPathGuard.EnsureOutsideGitWorkingTree(Path.GetDirectoryName(ledgerPath)!, "The issue ledger folder");
        var outputPath = Path.GetFullPath(p.Require("out"));
        if (File.Exists(outputPath)) throw new LicenseToolException("The renewal output already exists; nothing was changed.");
        if (OutputPathGuard.IsInsideGitWorkingTree(Path.GetDirectoryName(outputPath)!))
            console.Error.WriteLine("Warning: the renewed license contains customer data and is inside a git working tree.");

        var sourcePath = Path.GetFullPath(p.Require("license"));
        var oldInfo = new FileInfo(sourcePath);
        if (!oldInfo.Exists || oldInfo.Length is 0 or > LicenseLimits.MaxFileBytes)
            throw new LicenseToolException("The previous license is missing or too large.");
        var oldBytes = File.ReadAllBytes(sourcePath);
        var now = LicenseIssuer.Truncate(time.GetUtcNow());
        var oldVerification = new LicenseVerifier(trustedKeys, new PinnedTimeProvider(now)).Verify(oldBytes);
        var previous = oldVerification.Payload;
        if (previous is null || oldVerification.Status is not (LicenseStatus.Valid or LicenseStatus.ValidInGrace or LicenseStatus.Expired))
            throw new LicenseToolException("The previous license is not an authentic, eligible license.", 2);
        if (previous.ExpiresAt is null)
            throw new LicenseToolException("A perpetual license is not eligible for renewal.");
        if (previous.InstallationId is null)
            throw new LicenseToolException("Renewal requires the original installation-bound license.");

        var snapshotPath = Path.GetFullPath(p.Require("revocations"));
        var snapshotInfo = new FileInfo(snapshotPath);
        if (!snapshotInfo.Exists || snapshotInfo.Length is 0 or > SignedRevocationListVerifier.MaxFileBytes)
            throw new LicenseToolException("A current signed revocation snapshot is required for renewal.", 2);
        var snapshotResult = new SignedRevocationListVerifier(trustedKeys).Verify(File.ReadAllBytes(snapshotPath));
        if (!snapshotResult.IsValid || snapshotResult.List is null
            || snapshotResult.List.NextUpdate <= now || snapshotResult.List.IssuedAt > now.AddMinutes(5))
            throw new LicenseToolException("The supplied revocation snapshot is invalid or stale; renewal is refused.", 2);
        if (snapshotResult.List.RevokedLicenses.Any(x => string.Equals(x.LicenseId, previous.LicenseId, StringComparison.Ordinal)))
            throw new LicenseToolException("A revoked license cannot be renewed.", 2);

        using var ledgerLock = LedgerFileLock.Acquire(ledgerPath);
        var recorded = IssueLedger.FindById(ledgerPath, previous.LicenseId, ledgerLock)
            ?? throw new LicenseToolException("The previous license is not in the issue ledger; renewal is refused.", 2);
        if (recorded.CustomerId is null || recorded.ActivationRequestId is null || recorded.IssuedBy is null
            || !string.Equals(recorded.Licensee, previous.Licensee, StringComparison.Ordinal)
            || !string.Equals(recorded.InstallationId, previous.InstallationId, StringComparison.Ordinal))
            throw new LicenseToolException("The ledger has no complete customer/request binding for this license.", 2);
        var fileHash = Convert.ToHexString(SHA256.HashData(oldBytes)).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(fileHash), Encoding.ASCII.GetBytes(recorded.FileSha256)))
            throw new LicenseToolException("The license file does not match its issue-ledger hash; renewal is refused.", 2);

        var newLicenseId = p.Require("license-id");
        IssueLedger.EnsureNotIssued(ledgerPath, newLicenseId, ledgerLock);
        IssueLedger.EnsureNotRenewed(ledgerPath, previous.LicenseId, ledgerLock);
        var expiresAt = ParseTime("expires", p.Require("expires"), time);
        var request = LicenseIssuer.Validate(new LicenseRequest(
            LicenseId: newLicenseId,
            Licensee: previous.Licensee,
            IssuedAt: now,
            NotBefore: now,
            ExpiresAt: expiresAt,
            GraceDays: previous.GraceDays,
            MaxInstallations: previous.MaxInstallations,
            Product: previous.Product,
            InstallationId: previous.InstallationId), allowOtherProduct: false);

        using var signer = KeyStore.Load(p.Require("key"), ReadPassphrase(console, "Signer passphrase: "));
        var matches = trustedKeys.Where(k => string.Equals(k.KeyId, signer.KeyId, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1 || !matches[0].SubjectPublicKeyInfo.AsSpan().SequenceEqual(signer.SubjectPublicKeyInfo))
            throw new LicenseToolException("The signer does not match the authorized ERP trust set. Nothing was renewed.", 2);

        var renewedBytes = LicenseIssuer.Issue(request, signer);
        var selfCheck = new LicenseVerifier(trustedKeys, new PinnedTimeProvider(now)).Verify(renewedBytes);
        if (selfCheck.Status != LicenseStatus.Valid || selfCheck.Payload is not { } renewedPayload
            || renewedPayload.InstallationId != previous.InstallationId || renewedPayload.Licensee != previous.Licensee)
            throw new LicenseToolException("The renewed license failed its trust/binding check.", 2);

        var undo = new List<(string Path, byte[]? Original)>();
        try
        {
            undo.Add((outputPath, null));
            WriteAtomicNew(outputPath, renewedBytes);
            IssueLedger.Append(ledgerPath, request, signer.KeyId, renewedBytes, time.GetUtcNow(), ledgerLock,
                customerId: recorded.CustomerId, issuedBy: Environment.UserName,
                renewedFromLicenseId: previous.LicenseId);
        }
        catch
        {
            RollbackOutputs(undo, console);
            throw;
        }

        console.Out.WriteLine($"Renewed {previous.LicenseId} as {request.LicenseId} for customer {recorded.CustomerId}");
        console.Out.WriteLine($"  installation: {request.InstallationId}");
        console.Out.WriteLine($"  expires      : {LicenseIssuer.FormatTimestamp(request.ExpiresAt!.Value)}");
        console.Out.WriteLine($"  authorized by: {Environment.UserName}");
        console.Out.WriteLine($"  output       : {outputPath}");
        console.Out.WriteLine("  trust check  : passed against supplied ERP keys and current signed revocation snapshot");
        return 0;
    }

    // ---- revocation-snapshot ----

    private static int RevocationSnapshot(string[] rest, IToolConsole console, TimeProvider time)
    {
        var p = ArgParser.Parse(rest,
            ["key", "public-keys", "out", "previous", "revoke-id", "reason", "valid-days"], []);
        var publicKeysPath = Path.GetFullPath(p.Require("public-keys"));
        var keys = PublicKeyExport.Read(publicKeysPath);
        var output = Path.GetFullPath(p.Require("out"));
        OutputPathGuard.EnsureOutsideGitWorkingTree(Path.GetDirectoryName(output)!, "The revocation snapshot folder");

        byte[]? previous = null;
        if (p.Optional("previous") is { } previousPath)
        {
            previousPath = Path.GetFullPath(previousPath);
            var info = new FileInfo(previousPath);
            if (!info.Exists || info.Length is 0 or > SignedRevocationListVerifier.MaxFileBytes)
                throw new LicenseToolException("The previous snapshot is missing or too large.");
            previous = File.ReadAllBytes(previousPath);
        }

        var validDays = ParseInt("valid-days", p.Optional("valid-days"), 7);
        var revokeId = p.Optional("revoke-id");
        var reason = p.Optional("reason");
        using var signer = KeyStore.Load(p.Require("key"), ReadPassphrase(console, "Signer passphrase: "));
        var snapshot = RevocationSnapshotIssuer.Issue(keys, signer, time.GetUtcNow(), validDays,
            previous, revokeId, reason);
        WriteNew(output, snapshot, false, "The revocation snapshot");
        var verified = new SignedRevocationListVerifier(keys).Verify(snapshot);
        if (!verified.IsValid || verified.List is null)
            throw new LicenseToolException("The output snapshot failed verification after writing.", 2);
        console.Out.WriteLine($"Created signed revocation snapshot {verified.List.ListId}");
        console.Out.WriteLine($"  key id     : {signer.KeyId}");
        console.Out.WriteLine($"  revoked ids: {verified.List.RevokedLicenses.Count}");
        console.Out.WriteLine($"  next update: {LicenseIssuer.FormatTimestamp(verified.List.NextUpdate)}");
        console.Out.WriteLine($"  output     : {output}");
        console.Out.WriteLine($"  operator   : {Environment.UserName}");
        return 0;
    }

    // ---- helpers ----

    private static string? ParseInstallation(string? text)
    {
        if (text is null) return null;
        if (InstallationIds.TryParseRequestCode(text, out var id)) return id;
        throw new LicenseToolException("--installation must be the customer request code (FCR1-...).");
    }

    private static string ReadPassphrase(IToolConsole console, string prompt) =>
        console.ReadPassphrase(prompt)
        ?? throw new LicenseToolException("The passphrase must be typed interactively (it cannot come from a pipe, argument or variable).");

    private static DateTimeOffset ParseTime(string name, string text, TimeProvider time)
    {
        if (text == "now") return LicenseIssuer.Truncate(time.GetUtcNow());
        if (!LicenseIssuer.TryParseTimestamp(text, out var value))
            throw new LicenseToolException($"--{name} must be 'now', yyyy-MM-dd, or yyyy-MM-ddTHH:mm:ssZ.");
        return value;
    }

    private static int ParseInt(string name, string? text, int defaultValue)
    {
        if (text is null) return defaultValue;
        return int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new LicenseToolException($"--{name} must be a whole number.");
    }

    private static void WriteTracked(
        string path, byte[] bytes, bool force, string what, List<(string Path, byte[]? Original)> undo)
    {
        var exists = File.Exists(path);
        if (exists && !force)
            throw new LicenseToolException($"{what} already exists: {path}. Use --force to replace it.");
        undo.Add((path, exists ? File.ReadAllBytes(path) : null));
        WriteNew(path, bytes, force, what);
    }

    private static void WriteAtomicNew(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
                BufferSize = 4096, Options = FileOptions.Asynchronous,
            };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(temp, options))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temp, path, overwrite: false);
        }
        catch
        {
            try { File.Delete(temp); } catch (IOException) { }
            throw;
        }
    }

    /// <summary>Undoes this run's output files: delete what it created, restore what --force replaced.</summary>
    private static void RollbackOutputs(List<(string Path, byte[]? Original)> undo, IToolConsole console)
    {
        for (var i = undo.Count - 1; i >= 0; i--)
        {
            var (path, original) = undo[i];
            try
            {
                if (original is null) { if (File.Exists(path)) File.Delete(path); }
                else File.WriteAllBytes(path, original);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                console.Error.WriteLine(
                    "Warning: could not undo " + path + " (" + ex.GetType().Name
                    + "). Check it manually; the license was NOT recorded in the ledger.");
            }
        }
    }

    private static void WriteNew(string path, byte[] bytes, bool force, string what)
    {
        if (!force && File.Exists(path))
            throw new LicenseToolException($"{what} already exists: {path}. Use --force to replace it.");
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, bytes);
    }

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("FlexiCore.LicenseTool (owner only: never ship this, never copy it to a customer machine)");
        writer.WriteLine();
        writer.WriteLine("  keygen        --key-id <id> --out-dir <absolute folder outside any repo> [--force]");
        writer.WriteLine("  provision-key --key <encrypted id.p8> --public-keys <authorized ERP key set> --out-dir <secure owner folder>");
        writer.WriteLine("  export-public --key <id.p8> [--out <file>] [--force]");
        writer.WriteLine("  build-keyset  --out <production-public-keys.json> [--force] <a.public.json> [<b.public.json> ...]");
        writer.WriteLine("  issue         --key <id.p8> --public-keys <ERP key set> --license-id <id> --licensee <name>");
        writer.WriteLine("                --expires <date|never> --out <file>");
        writer.WriteLine("                [--not-before <date|now>] [--issued-at <date|now>] [--grace <days>] [--installations <n>]");
        writer.WriteLine("                [--ledger <absolute file outside any repo>] [--force]");
        writer.WriteLine("  renew         --key <id.p8> --public-keys <ERP key set> --license <previous license>");
        writer.WriteLine("                --revocations <fresh signed snapshot> --ledger <issue ledger>");
        writer.WriteLine("                --license-id <new id> --expires <date> --out <new license file>");
        writer.WriteLine("  verify        <license file> --public-keys <key set json> [--at <date>]");
        writer.WriteLine("  revocation-snapshot --key <id.p8> --public-keys <authorized ERP key set> --out <snapshot file>");
        writer.WriteLine("                     [--previous <signed snapshot>] [--revoke-id <license id> --reason <text>] [--valid-days <1..31>]");
        writer.WriteLine("  make-test-vectors --out <folder> [--force]   (test vectors only; throwaway key, never persisted)");
        writer.WriteLine();
        writer.WriteLine("Dates: yyyy-MM-dd (midnight UTC) or yyyy-MM-ddTHH:mm:ssZ. Passphrases are typed at a prompt, never passed as arguments.");
        writer.WriteLine("Exit codes: 0 ok, 1 bad usage or input, 2 operation failed, 3 license not accepted (verify).");
    }
}
