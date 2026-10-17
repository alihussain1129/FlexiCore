using FlexiCore.Licensing;

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
            case "export-public": return ExportPublic(rest, console);
            case "build-keyset": return BuildKeySet(rest, console);
            case "issue": return Issue(rest, console, time);
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
            ["key", "license-id", "licensee", "issued-at", "not-before", "expires", "grace", "installations", "out", "ledger", "installation", "format"],
            ["force"]);

        var keyPath = p.Require("key");
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
        if (OutputPathGuard.IsInsideGitWorkingTree(Path.GetDirectoryName(outPath)!))
            console.Error.WriteLine("Warning: the license file is inside a git working tree. It names a customer; do not commit it.");

        using var signer = KeyStore.Load(keyPath, ReadPassphrase(console, "Passphrase: "));
        var file = LicenseIssuer.Issue(request, signer);

        WriteNew(outPath, file, force, "The license file");
        if (format is "code" or "both")
            WriteNew(outPath + ".code.txt", System.Text.Encoding.ASCII.GetBytes(ActivationCode.Encode(file)), force, "The activation code file");
        if (ledger is not null) IssueLedger.Append(ledger, request, signer.KeyId, file, time.GetUtcNow());

        console.Out.WriteLine($"Issued {request.LicenseId} to '{request.Licensee}' with key '{signer.KeyId}'");
        console.Out.WriteLine($"  not before : {LicenseIssuer.FormatTimestamp(request.NotBefore)}");
        console.Out.WriteLine($"  expires    : {(request.ExpiresAt is { } e ? LicenseIssuer.FormatTimestamp(e) : "never")}");
        console.Out.WriteLine($"  file       : {outPath}");
        console.Out.WriteLine("  self-check : passed (verified with the production verifier before writing)");
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
        writer.WriteLine("  export-public --key <id.p8> [--out <file>] [--force]");
        writer.WriteLine("  build-keyset  --out <production-public-keys.json> [--force] <a.public.json> [<b.public.json> ...]");
        writer.WriteLine("  issue         --key <id.p8> --license-id <id> --licensee <name> --expires <date|never> --out <file>");
        writer.WriteLine("                [--not-before <date|now>] [--issued-at <date|now>] [--grace <days>] [--installations <n>]");
        writer.WriteLine("                [--ledger <absolute file outside any repo>] [--force]");
        writer.WriteLine("  verify        <license file> --public-keys <key set json> [--at <date>]");
        writer.WriteLine("  make-test-vectors --out <folder> [--force]   (test vectors only; throwaway key, never persisted)");
        writer.WriteLine();
        writer.WriteLine("Dates: yyyy-MM-dd (midnight UTC) or yyyy-MM-ddTHH:mm:ssZ. Passphrases are typed at a prompt, never passed as arguments.");
        writer.WriteLine("Exit codes: 0 ok, 1 bad usage or input, 2 operation failed, 3 license not accepted (verify).");
    }
}