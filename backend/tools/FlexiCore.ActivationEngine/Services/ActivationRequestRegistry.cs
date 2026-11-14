using System.Text.Json;
using System.Text.Json.Serialization;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Services;

public enum ActivationRequestStatus { Pending, UnderReview, Approved, Rejected, Issued, Cancelled }

public sealed record ActivationRequestHistoryEvent(
    string Action,
    string Actor,
    DateTimeOffset At,
    string? Reason,
    string? LicenseId = null);

public sealed record ActivationRequestRecord(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string Product,
    string InstallationId,
    DateTimeOffset CreatedAt,
    ActivationRequestStatus Status,
    List<ActivationRequestHistoryEvent> History,
    string? LicenseId = null);

public interface IRequestReviewerAuthorization
{
    string CurrentActor { get; }
    void DemandAuthorized(string? actor);
}

/// <summary>
/// Current owner engine runs as a local single-user desktop tool. The OS account identifies the active local
/// operator; this is an audit identity within that local trust model, not multi-user or remote authentication.
/// </summary>
public sealed class LocalRequestReviewerAuthorization : IRequestReviewerAuthorization
{
    public string CurrentActor => Environment.UserName.Trim();

    public void DemandAuthorized(string? actor)
    {
        var current = CurrentActor;
        if (current.Length == 0 || !string.Equals(actor?.Trim(), current, StringComparison.Ordinal))
            throw new LicenseToolException("Request review is allowed only for the current local owner account.");
    }
}

/// <summary>
/// Local, versioned request decision store. The FCR1 checksum establishes code integrity only; it does not verify
/// customer ownership. Import and approval are explicit local-owner actions and every transition is retained.
/// </summary>
public sealed class ActivationRequestRegistry
{
    private const int SchemaVersion = 1;
    private const long MaxBytes = 16 * 1024 * 1024;
    private readonly OwnerPaths _paths;
    private readonly CustomerRegistry _customers;
    private readonly TimeProvider _time;
    private readonly IRequestReviewerAuthorization _reviewer;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter<ActivationRequestStatus>() },
    };

    private sealed record StoreDocument(int SchemaVersion, List<ActivationRequestRecord> Requests);

    public ActivationRequestRegistry(
        OwnerPaths paths,
        CustomerRegistry customers,
        TimeProvider time,
        IRequestReviewerAuthorization? reviewer = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _reviewer = reviewer ?? new LocalRequestReviewerAuthorization();
    }

    public string CurrentActor => _reviewer.CurrentActor;

    public ActivationRequestRecord Register(string? requestText, Guid customerId, string? actor)
    {
        _reviewer.DemandAuthorized(actor);
        var parsed = RequestService.Parse(requestText);
        using var gate = AcquireLock();
        var document = ReadStore();
        var customer = _customers.Get(customerId)
            ?? throw new LicenseToolException("The selected customer does not exist.");
        if (customer.Status != CustomerStatus.Active)
            throw new LicenseToolException("An inactive customer cannot submit a new activation request.");

        var existing = document.Requests.FirstOrDefault(r =>
            string.Equals(r.InstallationId, parsed.InstallationId, StringComparison.Ordinal));
        if (existing is not null)
        {
            if (existing.CustomerId != customer.Id)
                throw new LicenseToolException("This installation is already associated with a different customer.");
            return WithEffectiveIssueStatus(existing, gate);
        }

        var now = _time.GetUtcNow().ToUniversalTime();
        var request = new ActivationRequestRecord(
            Guid.NewGuid(), customer.Id, customer.Name, LicenseLimits.ExpectedProduct, parsed.InstallationId,
            now, ActivationRequestStatus.Pending,
            [new ActivationRequestHistoryEvent("Submitted", _reviewer.CurrentActor, now, "Request checksum validated; customer ownership requires independent review.")]);
        document.Requests.Add(request);
        WriteStore(document);
        return request;
    }

    public IReadOnlyList<ActivationRequestRecord> List(
        ActivationRequestStatus? status = null,
        Guid? customerId = null,
        string? search = null)
    {
        using var gate = AcquireLock();
        var requests = ReadStore().Requests.Select(r => WithEffectiveIssueStatus(r, gate));
        var query = requests.AsEnumerable();
        if (status is { } filter) query = query.Where(r => r.Status == filter);
        if (customerId is { } customer) query = query.Where(r => r.CustomerId == customer);
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
            query = query.Where(r => r.Id.ToString("D").Contains(term, StringComparison.OrdinalIgnoreCase)
                || r.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || r.InstallationId.Contains(term, StringComparison.OrdinalIgnoreCase));
        return query.OrderByDescending(r => r.CreatedAt).ToArray();
    }

    public ActivationRequestRecord? Find(Guid requestId)
    {
        using var gate = AcquireLock();
        var record = ReadStore().Requests.SingleOrDefault(r => r.Id == requestId);
        return record is null ? null : WithEffectiveIssueStatus(record, gate);
    }

    public ActivationRequestRecord Transition(
        Guid requestId,
        ActivationRequestStatus target,
        string? actor,
        string? reason = null)
    {
        _reviewer.DemandAuthorized(actor);
        using var gate = AcquireLock();
        var document = ReadStore();
        var index = document.Requests.FindIndex(r => r.Id == requestId);
        if (index < 0) throw new LicenseToolException("Activation request was not found.");
        var current = WithEffectiveIssueStatus(document.Requests[index], gate);
        var cleanReason = reason?.Trim();
        if (target is ActivationRequestStatus.Rejected or ActivationRequestStatus.Cancelled
            && string.IsNullOrWhiteSpace(cleanReason))
            throw new LicenseToolException("A reason is required for rejection or cancellation.");
        if (cleanReason?.Length > 500) throw new LicenseToolException("The decision reason cannot exceed 500 characters.");

        var action = (current.Status, target) switch
        {
            (ActivationRequestStatus.Pending, ActivationRequestStatus.UnderReview) => "ReviewStarted",
            (ActivationRequestStatus.UnderReview, ActivationRequestStatus.Approved) => "Approved",
            (ActivationRequestStatus.UnderReview, ActivationRequestStatus.Rejected) => "Rejected",
            (ActivationRequestStatus.Pending or ActivationRequestStatus.UnderReview, ActivationRequestStatus.Cancelled) => "Cancelled",
            _ => throw new LicenseToolException($"Request cannot move from {current.Status} to {target}.")
        };

        if (target == ActivationRequestStatus.Approved)
        {
            var customer = _customers.Get(current.CustomerId);
            if (customer is null || customer.Status != CustomerStatus.Active
                || !string.Equals(customer.Name, current.CustomerName, StringComparison.Ordinal))
                throw new LicenseToolException("The linked customer is missing, inactive, or no longer matches this request.");
        }

        var now = _time.GetUtcNow().ToUniversalTime();
        var updated = current with
        {
            Status = target,
            History = [.. current.History, new ActivationRequestHistoryEvent(action, _reviewer.CurrentActor, now, cleanReason)]
        };
        document.Requests[index] = updated;
        WriteStore(document);
        return updated;
    }

    internal LedgerFileLock AcquireLock() => LedgerFileLock.Acquire(_paths.ActivationRequestsPath);

    internal ActivationRequestRecord GetApprovedForIssue(Guid requestId, LedgerFileLock requestLock)
    {
        ArgumentNullException.ThrowIfNull(requestLock);
        requestLock.EnsureFor(_paths.ActivationRequestsPath);
        var request = ReadStore().Requests.SingleOrDefault(r => r.Id == requestId)
            ?? throw new LicenseToolException("Approved activation request was not found.");
        request = WithEffectiveIssueStatus(request, requestLock);
        if (request.Status != ActivationRequestStatus.Approved)
            throw new LicenseToolException($"Only an approved activation request can be issued; current status is {request.Status}.");
        return request;
    }

    internal void MarkIssued(Guid requestId, Guid customerId, string licenseId, string actor, LedgerFileLock requestLock)
    {
        requestLock.EnsureFor(_paths.ActivationRequestsPath);
        _reviewer.DemandAuthorized(actor);
        var document = ReadStore();
        var index = document.Requests.FindIndex(r => r.Id == requestId);
        if (index < 0) throw new LicenseToolException("Activation request disappeared during issuance.");
        var current = document.Requests[index];
        if (current.CustomerId != customerId || current.Status != ActivationRequestStatus.Approved)
            throw new LicenseToolException("Activation request changed before issuance could be recorded.");
        var now = _time.GetUtcNow().ToUniversalTime();
        document.Requests[index] = current with
        {
            Status = ActivationRequestStatus.Issued,
            LicenseId = licenseId,
            History = [.. current.History, new ActivationRequestHistoryEvent("Issued", _reviewer.CurrentActor, now, null, licenseId)]
        };
        WriteStore(document);
    }

    public void DemandAuthorizedReviewer(string? actor) => _reviewer.DemandAuthorized(actor);

    private ActivationRequestRecord WithEffectiveIssueStatus(ActivationRequestRecord request, LedgerFileLock requestLock)
    {
        requestLock.EnsureFor(_paths.ActivationRequestsPath);
        var ledger = LedgerReader.Read(_paths.LedgerPath);
        if (ledger.Problem is not null || ledger.UnreadableLines > 0)
            throw new LicenseToolException("Issue history is unreadable; request status cannot be verified safely.");
        var issued = ledger.Entries.Where(e => e.ActivationRequestId == request.Id).ToArray();
        if (issued.Length > 1) throw new LicenseToolException("Multiple licenses reference one activation request; manual recovery is required.");
        if (issued.Length == 0) return request;
        var entry = issued[0];
        if (entry.CustomerId != request.CustomerId
            || !string.Equals(entry.Licensee, request.CustomerName, StringComparison.Ordinal)
            || !string.Equals(entry.InstallationId, request.InstallationId, StringComparison.Ordinal))
            throw new LicenseToolException("Issued license metadata does not match the activation request; manual recovery is required.");
        if (request.Status == ActivationRequestStatus.Issued && request.LicenseId == entry.LicenseId) return request;
        var events = request.History.Any(h => h.Action == "Issued")
            ? request.History
            : [.. request.History, new ActivationRequestHistoryEvent("Issued", entry.IssuedBy ?? "Unknown local operator", entry.RecordedAt, "Recovered from the durable issue ledger.", entry.LicenseId)];
        return request with { Status = ActivationRequestStatus.Issued, LicenseId = entry.LicenseId, History = events };
    }

    private StoreDocument ReadStore()
    {
        if (!File.Exists(_paths.ActivationRequestsPath)) return new StoreDocument(SchemaVersion, []);
        if (new FileInfo(_paths.ActivationRequestsPath).Length > MaxBytes)
            throw new LicenseToolException("Activation request registry exceeds its safety limit.");
        StoreDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<StoreDocument>(File.ReadAllBytes(_paths.ActivationRequestsPath), JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new LicenseToolException("Activation request registry could not be read safely; the original file was preserved.", 2);
        }
        if (document is null || document.SchemaVersion != SchemaVersion || document.Requests is null)
            throw new LicenseToolException("Activation request registry version or structure is unsupported; the original file was preserved.", 2);

        var ids = new HashSet<Guid>();
        var installations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var request in document.Requests)
        {
            if (request.Id == Guid.Empty || request.CustomerId == Guid.Empty || !ids.Add(request.Id)
                || !installations.Add(request.InstallationId)
                || !InstallationIds.IsValid(request.InstallationId)
                || request.Product != LicenseLimits.ExpectedProduct
                || string.IsNullOrWhiteSpace(request.CustomerName)
                || !Enum.IsDefined(request.Status)
                || request.History is null || request.History.Count == 0
                || request.History.Any(h => string.IsNullOrWhiteSpace(h.Action) || string.IsNullOrWhiteSpace(h.Actor)
                    || h.At.Offset != TimeSpan.Zero || h.Reason?.Length > 500))
                throw new LicenseToolException("Activation request registry contains invalid records; the original file was preserved.", 2);
            if (request.Status == ActivationRequestStatus.Issued && string.IsNullOrWhiteSpace(request.LicenseId))
                throw new LicenseToolException("An issued request has no license ID; the original file was preserved.", 2);
        }
        return document;
    }

    private void WriteStore(StoreDocument document)
    {
        var directory = Path.GetDirectoryName(_paths.ActivationRequestsPath)!;
        Directory.CreateDirectory(directory);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        if (bytes.Length > MaxBytes) throw new LicenseToolException("Activation request registry would exceed its safety limit.");
        var temp = Path.Combine(directory, ".activation-requests-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(temp, options))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temp, _paths.ActivationRequestsPath, true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
        }
    }
}
