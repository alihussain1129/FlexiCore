using System.Collections.ObjectModel;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.ViewModels;

public sealed record ApprovedActivation(ActivationRequestRecord Request, CustomerRecord Customer);

public sealed class ActivationRequestsViewModel : PageViewModel
{
    private readonly ActivationRequestRegistry _requests;
    private readonly CustomerRegistry _customers;
    private readonly IDialogService _dialogs;
    private IReadOnlyList<ActivationRequestRecord> _rows = Array.Empty<ActivationRequestRecord>();
    private ActivationRequestRecord? _selected;
    private CustomerRecord? _selectedCustomer;
    private string _requestText = "";
    private string _search = "";
    private string _reason = "";
    private string _statusFilter = "All";

    public ActivationRequestsViewModel(ActivationRequestRegistry requests, CustomerRegistry customers, IDialogService dialogs)
        : base("Activation Requests", "Import checksum-validated requests, associate an active customer, and record explicit local-owner review decisions.", "\uE896")
    {
        _requests = requests;
        _customers = customers;
        _dialogs = dialogs;
        RegisterCommand = new RelayCommand(Register);
        ReviewCommand = new RelayCommand(() => Transition(ActivationRequestStatus.UnderReview), () => Selected?.Status == ActivationRequestStatus.Pending);
        ApproveCommand = new RelayCommand(() => Transition(ActivationRequestStatus.Approved), () => Selected?.Status == ActivationRequestStatus.UnderReview);
        RejectCommand = new RelayCommand(() => Transition(ActivationRequestStatus.Rejected), () => Selected?.Status == ActivationRequestStatus.UnderReview);
        CancelCommand = new RelayCommand(() => Transition(ActivationRequestStatus.Cancelled), () => Selected?.Status is ActivationRequestStatus.Pending or ActivationRequestStatus.UnderReview);
        IssueCommand = new RelayCommand(Issue, () => Selected?.Status == ActivationRequestStatus.Approved);
        RefreshCommand = new RelayCommand(Refresh);
    }

    public event Action<ApprovedActivation>? IssueRequested;
    public ObservableCollection<CustomerRecord> ActiveCustomers { get; } = new();
    public IReadOnlyList<string> StatusFilters { get; } = ["All", .. Enum.GetNames<ActivationRequestStatus>()];
    public RelayCommand RegisterCommand { get; }
    public RelayCommand ReviewCommand { get; }
    public RelayCommand ApproveCommand { get; }
    public RelayCommand RejectCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand IssueCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public IReadOnlyList<ActivationRequestRecord> Rows { get => _rows; private set => Set(ref _rows, value); }
    public bool HasNoRows => Rows.Count == 0;
    public ActivationRequestRecord? Selected
    {
        get => _selected;
        set { if (Set(ref _selected, value)) { OnPropertyChanged(nameof(HistoryText)); OnPropertyChanged(nameof(CustomerDetails)); CommandRequery.Invalidate(); } }
    }
    public CustomerRecord? SelectedCustomer { get => _selectedCustomer; set => Set(ref _selectedCustomer, value); }
    public string RequestText { get => _requestText; set => Set(ref _requestText, value); }
    public string SearchText { get => _search; set { if (Set(ref _search, value)) Refresh(); } }
    public string Reason { get => _reason; set => Set(ref _reason, value); }
    public string StatusFilter { get => _statusFilter; set { if (Set(ref _statusFilter, value)) Refresh(); } }
    public string Reviewer => _requests.CurrentActor;
    public string HistoryText => Selected is null
        ? "Select a request to inspect its review history."
        : string.Join(Environment.NewLine, Selected.History.Select(h =>
            $"{h.At:yyyy-MM-dd HH:mm:ss} UTC · {h.Action} · {h.Actor}" + (string.IsNullOrWhiteSpace(h.Reason) ? "" : " · " + h.Reason)
              + (h.LicenseId is null ? "" : " · License " + h.LicenseId)));
    public string CustomerDetails
    {
        get
        {
            if (Selected is null) return "";
            var customer = _customers.Get(Selected.CustomerId);
            return customer is null ? "Linked customer is unavailable."
                : $"{customer.Name} · {customer.Status} · {customer.Email ?? "no email on file"}";
        }
    }

    public override Task OnShownAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    public void Refresh()
    {
        try
        {
            var active = _customers.List().Where(c => c.Status == CustomerStatus.Active).ToArray();
            ActiveCustomers.Clear();
            foreach (var customer in active) ActiveCustomers.Add(customer);
            var filter = Enum.TryParse<ActivationRequestStatus>(StatusFilter, out var parsed) ? parsed : (ActivationRequestStatus?)null;
            Rows = _requests.List(filter, search: SearchText);
            if (Selected is not null) Selected = Rows.SingleOrDefault(r => r.Id == Selected.Id);
            OnPropertyChanged(nameof(HasNoRows));
            OnPropertyChanged(nameof(HistoryText));
            OnPropertyChanged(nameof(CustomerDetails));
            if (Rows.Count == 0) ClearStatus();
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, Describe(ex));
        }
    }

    public void ReportHandoffError(string message) => SetStatus(StatusKind.Error, message);

    private void Register()
    {
        try
        {
            if (SelectedCustomer is null) throw new LicenseToolException("Choose an active customer before importing the request.");
            var request = _requests.Register(RequestText, SelectedCustomer.Id, Reviewer);
            RequestText = "";
            Refresh();
            Selected = Rows.SingleOrDefault(r => r.Id == request.Id);
            SetStatus(StatusKind.Success, request.Status == ActivationRequestStatus.Pending
                ? "Request registered as Pending. Its checksum does not verify customer ownership; review that separately."
                : "This installation already has a request. Existing request history was preserved.");
        }
        catch (Exception ex) when (ex is LicenseToolException or IOException or UnauthorizedAccessException)
        {
            SetStatus(StatusKind.Error, Describe(ex));
        }
    }

    private void Transition(ActivationRequestStatus target)
    {
        if (Selected is null) return;
        var action = target switch
        {
            ActivationRequestStatus.UnderReview => "Move this request to Under Review?",
            ActivationRequestStatus.Approved => "Approve this request for license issuance? Confirm customer ownership and purchase authorization first.",
            ActivationRequestStatus.Rejected => "Reject this request? A reason is required.",
            ActivationRequestStatus.Cancelled => "Cancel this request? A reason is required.",
            _ => "Update this request?"
        };
        if (!_dialogs.Confirm("Confirm request decision", action)) return;
        try
        {
            Selected = _requests.Transition(Selected.Id, target, Reviewer, Reason);
            Reason = "";
            Refresh();
            SetStatus(StatusKind.Success, "Request status recorded: " + target + ".");
        }
        catch (Exception ex) when (ex is LicenseToolException or IOException or UnauthorizedAccessException)
        {
            SetStatus(StatusKind.Error, Describe(ex));
        }
    }

    private void Issue()
    {
        if (Selected is null) return;
        try
        {
            var customer = _customers.Get(Selected.CustomerId);
            if (customer is null || customer.Status != CustomerStatus.Active)
                throw new LicenseToolException("The linked customer is missing or inactive.");
            if (Selected.Status != ActivationRequestStatus.Approved)
                throw new LicenseToolException("Only approved requests can be sent to license issuance.");
            IssueRequested?.Invoke(new ApprovedActivation(Selected, customer));
        }
        catch (Exception ex) when (ex is LicenseToolException or IOException or UnauthorizedAccessException)
        {
            SetStatus(StatusKind.Error, Describe(ex));
        }
    }
}
