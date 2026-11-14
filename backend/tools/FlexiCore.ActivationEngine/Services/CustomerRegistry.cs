using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Services;

public enum CustomerStatus { Active, Inactive }

public sealed record CustomerRecord(
    Guid Id,
    string Name,
    string? LegalName,
    string? Email,
    string? Phone,
    CustomerStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CustomerInput(string Name, string? LegalName = null, string? Email = null, string? Phone = null);

/// <summary>
/// Durable, local-first customer registry for the owner Activation Engine. Existing license history remains in
/// the append-only issue ledger; the registry adds stable customer IDs without rewriting historical records.
/// </summary>
public sealed class CustomerRegistry
{
    private const int SchemaVersion = 1;
    private const long MaxBytes = 8 * 1024 * 1024;
    private readonly OwnerPaths _paths;
    private readonly TimeProvider _time;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter<CustomerStatus>() },
    };

    private sealed record StoreDocument(int SchemaVersion, List<CustomerRecord> Customers);

    public CustomerRegistry(OwnerPaths paths, TimeProvider time)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public IReadOnlyList<CustomerRecord> List()
    {
        using var gate = LedgerFileLock.Acquire(_paths.CustomersPath);
        return ReadStore().Customers.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public CustomerRecord? Get(Guid id)
    {
        using var gate = LedgerFileLock.Acquire(_paths.CustomersPath);
        return ReadStore().Customers.SingleOrDefault(c => c.Id == id);
    }

    public CustomerRecord Register(CustomerInput input)
    {
        var normalized = Validate(input);
        using var gate = LedgerFileLock.Acquire(_paths.CustomersPath);
        var document = ReadStore();
        EnsureEmailAvailable(document.Customers, normalized.Email, null);
        var now = _time.GetUtcNow();
        var customer = new CustomerRecord(Guid.NewGuid(), normalized.Name, normalized.LegalName,
            normalized.Email, normalized.Phone, CustomerStatus.Active, now, now);
        document.Customers.Add(customer);
        WriteStore(document);
        return customer;
    }

    public CustomerRecord Update(Guid id, CustomerInput input)
    {
        var normalized = Validate(input);
        using var gate = LedgerFileLock.Acquire(_paths.CustomersPath);
        var document = ReadStore();
        var index = document.Customers.FindIndex(c => c.Id == id);
        if (index < 0) throw new LicenseToolException("The selected customer no longer exists.");
        EnsureEmailAvailable(document.Customers, normalized.Email, id);
        var current = document.Customers[index];
        var updated = current with
        {
            Name = normalized.Name,
            LegalName = normalized.LegalName,
            Email = normalized.Email,
            Phone = normalized.Phone,
            UpdatedAt = _time.GetUtcNow(),
        };
        document.Customers[index] = updated;
        WriteStore(document);
        return updated;
    }

    public CustomerRecord SetStatus(Guid id, CustomerStatus status)
    {
        if (!Enum.IsDefined(status)) throw new LicenseToolException("Choose a valid customer status.");
        using var gate = LedgerFileLock.Acquire(_paths.CustomersPath);
        var document = ReadStore();
        var index = document.Customers.FindIndex(c => c.Id == id);
        if (index < 0) throw new LicenseToolException("The selected customer no longer exists.");
        var updated = document.Customers[index] with { Status = status, UpdatedAt = _time.GetUtcNow() };
        document.Customers[index] = updated;
        WriteStore(document);
        return updated;
    }

    private StoreDocument ReadStore()
    {
        if (!File.Exists(_paths.CustomersPath)) return new StoreDocument(SchemaVersion, []);
        var info = new FileInfo(_paths.CustomersPath);
        if (info.Length > MaxBytes) throw new LicenseToolException("Customer registry exceeds the 8 MB safety limit.");
        StoreDocument? store;
        try
        {
            store = JsonSerializer.Deserialize<StoreDocument>(File.ReadAllBytes(_paths.CustomersPath), JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new LicenseToolException("Customer registry could not be read safely; the original file was preserved.", 2);
        }
        if (store is null || store.SchemaVersion != SchemaVersion || store.Customers is null)
            throw new LicenseToolException("Customer registry version or structure is not supported; the original file was preserved.", 2);

        var ids = new HashSet<Guid>();
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var customer in store.Customers)
        {
            if (customer.Id == Guid.Empty || !ids.Add(customer.Id) || !Enum.IsDefined(customer.Status))
                throw new LicenseToolException("Customer registry contains invalid or duplicate records; the original file was preserved.", 2);
            var checkedInput = Validate(new CustomerInput(customer.Name, customer.LegalName, customer.Email, customer.Phone));
            if (checkedInput.Email is not null && !emails.Add(checkedInput.Email))
                throw new LicenseToolException("Customer registry contains duplicate email addresses; the original file was preserved.", 2);
        }
        return store;
    }

    private void WriteStore(StoreDocument document)
    {
        var directory = Path.GetDirectoryName(_paths.CustomersPath)!;
        Directory.CreateDirectory(directory);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        if (bytes.Length > MaxBytes) throw new LicenseToolException("Customer registry would exceed the 8 MB safety limit.");
        var temp = Path.Combine(directory, ".customers-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
            };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(temp, options))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temp, _paths.CustomersPath, true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
        }
    }

    private static CustomerInput Validate(CustomerInput? input)
    {
        if (input is null) throw new LicenseToolException("Customer details are required.");
        var name = Clean(input.Name);
        if (name.Length is < 2 or > 160 || name.Any(char.IsControl))
            throw new LicenseToolException("Customer name must contain 2–160 printable characters.");
        var legalName = CleanOptional(input.LegalName, 200, "Legal name");
        var email = CleanOptional(input.Email, 254, "Email address");
        if (email is not null)
        {
            try
            {
                var parsed = new MailAddress(email);
                if (!string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase)) throw new FormatException();
            }
            catch (FormatException)
            {
                throw new LicenseToolException("Enter a valid email address or leave it blank.");
            }
        }
        var phone = CleanOptional(input.Phone, 40, "Phone number");
        if (phone?.Any(char.IsControl) == true) throw new LicenseToolException("Phone number cannot contain control characters.");
        return new CustomerInput(name, legalName, email, phone);
    }

    private static string Clean(string? value) => (value ?? string.Empty).Trim();

    private static string? CleanOptional(string? value, int maxLength, string label)
    {
        var clean = Clean(value);
        if (clean.Length == 0) return null;
        if (clean.Length > maxLength || clean.Any(char.IsControl))
            throw new LicenseToolException($"{label} must be at most {maxLength} printable characters.");
        return clean;
    }

    private static void EnsureEmailAvailable(IEnumerable<CustomerRecord> customers, string? email, Guid? exceptId)
    {
        if (email is not null && customers.Any(c => c.Id != exceptId && string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase)))
            throw new LicenseToolException("That email address is already registered to another customer.");
    }
}
