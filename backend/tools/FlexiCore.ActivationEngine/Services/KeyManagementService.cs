using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Services;

public sealed class RegistryKey
{
    public string KeyId { get; set; } = "";
    public bool Retired { get; set; }
    public string? RetiredAt { get; set; }
    public bool PendingErp { get; set; }
    public string? Note { get; set; }
}

public sealed class RegistryFile
{
    public string? ErpKeyFile { get; set; }
    public List<RegistryKey> Keys { get; set; } = new();
}

public sealed record KeyEvent(DateTimeOffset When, string KeyId, string Action, string Detail);

/// <summary>
/// Owner-side bookkeeping for signing keys: which are retired, which are new and not yet in the ERP, and an
/// append-only event log. Contains no key material. It lives beside the ledger, outside any git working tree.
/// </summary>
public sealed class KeyRegistry
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly string _eventsPath;

    public KeyRegistry(OwnerPaths paths)
    {
        _filePath = Path.Combine(paths.Root, "key-registry.json");
        _eventsPath = Path.Combine(paths.Root, "key-events.jsonl");
    }

    public RegistryFile Load(out string? problem)
    {
        problem = null;
        try
        {
            if (!File.Exists(_filePath)) return new RegistryFile();
            if (new FileInfo(_filePath).Length > 1024 * 1024)
            {
                problem = "The key registry is too large and was ignored.";
                return new RegistryFile();
            }
            var data = JsonSerializer.Deserialize<RegistryFile>(File.ReadAllText(_filePath, Encoding.UTF8), Options);
            return data ?? new RegistryFile();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            problem = "The key registry could not be read (" + ex.GetType().Name + "). Key status was not applied.";
            return new RegistryFile();
        }
    }

    /// <summary>Loads for a change. A damaged registry is never overwritten.</summary>
    public RegistryFile LoadForEdit()
    {
        var data = Load(out var problem);
        if (problem is not null) throw new LicenseToolException(problem + " Nothing was changed.", 2);
        return data;
    }

    private void Save(RegistryFile data)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        var temp = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temp, JsonSerializer.Serialize(data, Options), new UTF8Encoding(false));
            File.Move(temp, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(temp); } catch (Exception) { }
            throw new LicenseToolException("The key registry could not be saved (" + ex.GetType().Name + ").", 2);
        }
    }

    private static RegistryKey Entry(RegistryFile data, string keyId)
    {
        var entry = data.Keys.FirstOrDefault(k => k.KeyId == keyId);
        if (entry is null)
        {
            entry = new RegistryKey { KeyId = keyId };
            data.Keys.Add(entry);
        }
        return entry;
    }

    public void SetRetired(string keyId, bool retired, DateTimeOffset now)
    {
        var data = LoadForEdit();
        var entry = Entry(data, keyId);
        entry.Retired = retired;
        entry.RetiredAt = retired ? LicenseIssuer.FormatTimestamp(now) : null;
        Save(data);
    }

    public void SetPending(string keyId, bool pending)
    {
        var data = LoadForEdit();
        Entry(data, keyId).PendingErp = pending;
        Save(data);
    }

    public void SetErpKeyFile(string path)
    {
        var data = LoadForEdit();
        data.ErpKeyFile = path;
        Save(data);
    }

    public void LogEvent(DateTimeOffset when, string keyId, string action, string detail)
    {
        try
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("when", LicenseIssuer.FormatTimestamp(when));
                writer.WriteString("keyId", keyId);
                writer.WriteString("action", action);
                writer.WriteString("detail", detail);
                writer.WriteEndObject();
            }
            Directory.CreateDirectory(Path.GetDirectoryName(_eventsPath)!);
            File.AppendAllText(_eventsPath, Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine, new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The log is a convenience record. A failed log line never blocks key work.
        }
    }

    public IReadOnlyList<KeyEvent> ReadEvents(int max)
    {
        var list = new List<KeyEvent>();
        try
        {
            if (!File.Exists(_eventsPath) || new FileInfo(_eventsPath).Length > 5 * 1024 * 1024) return list;
            foreach (var line in File.ReadLines(_eventsPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var r = doc.RootElement;
                    if (r.ValueKind != JsonValueKind.Object) continue;
                    if (!LicenseIssuer.TryParseTimestamp(r.GetProperty("when").GetString() ?? "", out var at)) continue;
                    list.Add(new KeyEvent(
                        at,
                        r.GetProperty("keyId").GetString() ?? "",
                        r.GetProperty("action").GetString() ?? "",
                        r.GetProperty("detail").GetString() ?? ""));
                }
                catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
                {
                    // Skip a damaged line.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return list;
        }
        return list.OrderByDescending(e => e.When).Take(max).ToList();
    }
}

public sealed record ManagedKey(
    string KeyId,
    DateTimeOffset CreatedUtc,
    string? Fingerprint,
    string? Problem,
    bool Retired,
    bool PendingErp,
    bool? InErpFile,
    int Licenses,
    DateTimeOffset? FirstUsed,
    DateTimeOffset? LastUsed,
    int Outstanding,
    string? PublicKeyBase64)
{
    public bool IsTest =>
        KeyId.StartsWith("dev-", StringComparison.Ordinal) || KeyId.StartsWith("test", StringComparison.Ordinal);

    public bool Usable => Problem is null && !Retired;

    /// <summary>A retired key whose licenses are all expired. Only then is it safe to drop its public key from the ERP.</summary>
    public bool SafeToRemoveFromErp => Retired && Outstanding == 0;

    public string ErpText => InErpFile switch { true => "Yes", false => "No", _ => "Unknown" };

    public string State =>
        Retired ? "Retired"
        : Problem is not null ? "Problem"
        : PendingErp && InErpFile != true ? "New, not in ERP yet"
        : "Active";
}

public sealed class KeyOverview
{
    public KeyOverview(
        IReadOnlyList<ManagedKey> keys, string? problem, string? erpKeyFile, string? erpFileProblem,
        ManagedKey? recommended, string notice)
    {
        Keys = keys;
        Problem = problem;
        ErpKeyFile = erpKeyFile;
        ErpFileProblem = erpFileProblem;
        Recommended = recommended;
        Notice = notice;
    }

    public IReadOnlyList<ManagedKey> Keys { get; }
    public string? Problem { get; }
    public string? ErpKeyFile { get; }
    public string? ErpFileProblem { get; }
    public ManagedKey? Recommended { get; }
    public string Notice { get; }

    public bool IsSelectable(string keyId) => Keys.Any(k => k.KeyId == keyId && Selectable(k));

    /// <summary>
    /// Usable (not retired, no problem), not known to be missing from the ERP key file (test keys excepted),
    /// and not a brand-new key that has not been confirmed in the ERP yet.
    /// </summary>
    internal static bool Selectable(ManagedKey k) =>
        k.Usable
        && (k.InErpFile != false || k.IsTest)
        && !(k.PendingErp && k.InErpFile != true);
}

/// <summary>
/// Chooses the signing key automatically and keeps its history: usage comes from the issue ledger, status from the
/// key registry, and "built into the ERP" from comparing public keys with the ERP key file.
/// </summary>
public sealed class KeyManagementService
{
    public const int RotateAfterDays = 365;
    public const int RotateAfterLicenses = 500;

    private readonly OwnerPaths _paths;
    private readonly KeyService _keys;
    private readonly TimeProvider _time;
    private readonly KeyRegistry _registry;
    private readonly Func<string?> _discover;

    public KeyManagementService(OwnerPaths paths, KeyService keys, TimeProvider time, Func<string?>? discoverErpFile = null)
    {
        _paths = paths;
        _keys = keys;
        _time = time;
        _registry = new KeyRegistry(paths);
        _discover = discoverErpFile ?? DiscoverErpKeyFile;
    }

    /// <summary>Development convenience: finds the ERP key file by walking up from the Engine's folder.</summary>
    public static string? DiscoverErpKeyFile()
    {
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "src", "FlexiCore.Licensing", "Keys", "production-public-keys.json");
                if (File.Exists(candidate)) return candidate;
            }
        }
        catch (Exception)
        {
            // Not found is a normal answer.
        }
        return null;
    }

    public KeyOverview Load()
    {
        var now = _time.GetUtcNow();
        var infos = _keys.List(out var listProblem);
        var registry = _registry.Load(out var registryProblem);
        var ledger = LedgerReader.Read(_paths.LedgerPath);
        var erpFile = registry.ErpKeyFile ?? _discover();

        string? erpProblem = null;
        Dictionary<string, byte[]>? erpKeys = null;
        if (erpFile is not null)
        {
            try
            {
                erpKeys = PublicKeyExport.Read(erpFile)
                    .ToDictionary(k => k.KeyId, k => k.SubjectPublicKeyInfo, StringComparer.Ordinal);
            }
            catch (Exception ex) when (ex is LicenseToolException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                erpProblem = "The ERP key file could not be read.";
            }
        }

        var rows = new List<ManagedKey>();
        foreach (var info in infos)
        {
            var entry = registry.Keys.FirstOrDefault(k => k.KeyId == info.KeyId);
            var used = ledger.Entries.Where(e => e.KeyId == info.KeyId).ToList();

            byte[]? spki = null;
            if (info.PublicKeyPath is not null)
            {
                try { spki = PublicKeyExport.Read(info.PublicKeyPath).FirstOrDefault(k => k.KeyId == info.KeyId)?.SubjectPublicKeyInfo; }
                catch (Exception ex) when (ex is LicenseToolException or IOException or UnauthorizedAccessException or ArgumentException)
                {
                    spki = null;
                }
            }

            bool? inErp = null;
            if (erpKeys is not null && spki is not null)
                inErp = erpKeys.TryGetValue(info.KeyId, out var theirs) && theirs.AsSpan().SequenceEqual(spki);

            rows.Add(new ManagedKey(
                info.KeyId,
                info.CreatedUtc,
                info.Fingerprint,
                info.Problem,
                entry?.Retired ?? false,
                entry?.PendingErp ?? false,
                inErp,
                used.Count,
                used.Count == 0 ? null : (DateTimeOffset?)used.Min(e => e.RecordedAt),
                used.Count == 0 ? null : (DateTimeOffset?)used.Max(e => e.RecordedAt),
                used.Count(e => e.StatusAt(now) != TermStatus.Expired),
                spki is null ? null : Convert.ToBase64String(spki)));
        }

        var pick = rows.Where(KeyOverview.Selectable)
            .OrderBy(Rank)
            .ThenByDescending(k => k.CreatedUtc)
            .FirstOrDefault();

        var notice = BuildNotice(rows, pick, erpFile, erpProblem, listProblem, registryProblem, now);
        return new KeyOverview(rows, listProblem ?? registryProblem, erpFile, erpProblem, pick, notice);
    }

    private static int Rank(ManagedKey k) => k.IsTest ? 2 : k.InErpFile == true ? 0 : 1;

    private static string BuildNotice(
        IReadOnlyList<ManagedKey> rows, ManagedKey? pick, string? erpFile, string? erpProblem,
        string? listProblem, string? registryProblem, DateTimeOffset now)
    {
        var parts = new List<string>();
        if (listProblem is not null) parts.Add(listProblem);
        if (registryProblem is not null) parts.Add(registryProblem);

        if (rows.Count > 0 && pick is null)
        {
            if (rows.All(k => k.Retired))
                parts.Add("All signing keys are retired. Create a new key on the Key Management page.");
            else if (erpFile is not null && erpProblem is null && rows.Any(k => k.Usable && k.InErpFile == false))
                parts.Add("None of your signing keys is in the ERP key file, so a license signed now would be refused by the ERP. "
                          + "Add a public key to that file, rebuild the ERP, then press Check ERP key file on the Key Management page. For testing only, create a key whose id starts with dev- on the Signing Keys page.");
            else
                parts.Add("No signing key can be used automatically. Open the Key Management page to see why.");
        }

        if (pick is not null)
        {
            if (pick.IsTest)
                parts.Add("Test key: real ERP builds will refuse licenses signed with it.");
            else if (pick.InErpFile is null)
                parts.Add("The ERP key file was not found, so this key cannot be confirmed as built into the ERP. Choose the file on the Key Management page.");

            var advice = RotationAdvice(pick, now);
            if (advice is not null) parts.Add(advice);
        }

        return string.Join(" ", parts);
    }

    public static string? RotationAdvice(ManagedKey key, DateTimeOffset now)
    {
        if (key.Retired) return null;
        if ((now - key.CreatedUtc).TotalDays > RotateAfterDays)
            return "Created more than " + RotateAfterDays.ToString(CultureInfo.InvariantCulture) + " days ago. Consider rotating.";
        if (key.Licenses >= RotateAfterLicenses)
            return "Has signed " + key.Licenses.ToString(CultureInfo.InvariantCulture) + " licenses. Consider rotating.";
        return null;
    }

    public static string PublicKeySnippet(string keyId, string publicKeyBase64) =>
        "{\n  \"keyId\": \"" + keyId + "\",\n  \"publicKey\": \"" + publicKeyBase64 + "\"\n}";

    public void Retire(string keyId) =>
        SetRetired(keyId, true, "retired", "No new licenses will be signed with this key.");

    public void Reactivate(string keyId) =>
        SetRetired(keyId, false, "reactivated", "The key can sign licenses again.");

    private void SetRetired(string keyId, bool retired, string action, string detail)
    {
        KeyStore.ValidateKeyId(keyId);
        var now = _time.GetUtcNow();
        _registry.SetRetired(keyId, retired, now);
        _registry.LogEvent(now, keyId, action, detail);
    }

    public void MarkBuiltIntoErp(string keyId)
    {
        KeyStore.ValidateKeyId(keyId);
        _registry.SetPending(keyId, false);
        _registry.LogEvent(_time.GetUtcNow(), keyId, "marked-in-erp", "Confirmed by the owner as built into the ERP.");
    }

    public void SetErpKeyFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new LicenseToolException("The ERP key file path must be absolute.");
        var keys = PublicKeyExport.Read(path); // validates the file with the production key rules
        _registry.SetErpKeyFile(path);
        _registry.LogEvent(_time.GetUtcNow(), "-", "erp-key-file",
            Path.GetFileName(path) + " (" + keys.Count.ToString(CultureInfo.InvariantCulture) + " keys)");
    }

    /// <summary>Creates the next key with an unused id. It stays "new" until it is confirmed to be in the ERP.</summary>
    public string CreateRotatedKey(string passphrase, string confirmation)
    {
        _registry.LoadForEdit(); // refuse before any key is written if the registry is damaged
        var id = NextKeyId();
        _keys.Create(id, passphrase, confirmation);
        _registry.SetPending(id, true);
        _registry.LogEvent(_time.GetUtcNow(), id, "created", "Rotation key. Not used for signing until it is built into the ERP.");
        return id;
    }

    public string NextKeyId()
    {
        var year = _time.GetUtcNow().Year.ToString(CultureInfo.InvariantCulture);
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in _keys.List(out _)) taken.Add(k.KeyId);
        foreach (var k in _registry.Load(out _).Keys) taken.Add(k.KeyId);

        var erpFile = _registry.Load(out _).ErpKeyFile ?? _discover();
        if (erpFile is not null)
        {
            try { foreach (var k in PublicKeyExport.Read(erpFile)) taken.Add(k.KeyId); }
            catch (Exception ex) when (ex is LicenseToolException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Without the ERP list, the local keys alone decide.
            }
        }

        for (var c = 'a'; c <= 'z'; c++)
        {
            var id = "fc-" + year + "-" + c;
            if (!taken.Contains(id)) return id;
        }
        return "fc-" + year + "-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant();
    }

    public IReadOnlyList<KeyEvent> ReadEvents(int max) => _registry.ReadEvents(max);

    /// <summary>True when the id is used by a local key, the key registry or the ERP key file.</summary>
    public bool IsKeyIdTaken(string keyId)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in _keys.List(out _)) taken.Add(k.KeyId);
        var registry = _registry.Load(out _);
        foreach (var k in registry.Keys) taken.Add(k.KeyId);
        var erpFile = registry.ErpKeyFile ?? _discover();
        if (erpFile is not null)
        {
            try { foreach (var k in PublicKeyExport.Read(erpFile)) taken.Add(k.KeyId); }
            catch (Exception ex) when (ex is LicenseToolException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Without the ERP list, the local keys and the registry decide.
            }
        }
        return taken.Contains(keyId);
    }

    /// <summary>Call before creating a key by hand. Refuses a used id and a damaged registry.</summary>
    public void EnsureCanCreate(string keyId)
    {
        KeyStore.ValidateKeyId(keyId);
        _registry.LoadForEdit();
        if (IsKeyIdTaken(keyId))
            throw new LicenseToolException("The key id '" + keyId + "' is already used by a local key, the key registry or the ERP key file. Choose another id.");
    }

    /// <summary>Records a key created on the Signing Keys page. A non-test key waits until it is confirmed in the ERP.</summary>
    public void RecordManualKey(string keyId, bool isTest)
    {
        if (!isTest) _registry.SetPending(keyId, true);
        _registry.LogEvent(_time.GetUtcNow(), keyId, "created",
            isTest ? "Test key created on the Signing Keys page." : "Key created on the Signing Keys page. Not used for signing until it is built into the ERP.");
    }
}