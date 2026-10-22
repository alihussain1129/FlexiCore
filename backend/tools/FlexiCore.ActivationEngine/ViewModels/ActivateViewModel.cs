using FlexiCore.ActivationEngine.Services;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.ViewModels;

public sealed class ActivateViewModel : PageViewModel
{
    private readonly IDialogService _dialogs;
    private string _requestText = "";
    private ParsedRequest? _parsed;

    public event Action<string>? ProceedRequested;

    public ActivateViewModel(IDialogService dialogs)
        : base("Activate Installation", "Import a customer's installation request and check it.", "\uE896")
    {
        _dialogs = dialogs;
        VerifyCommand = new RelayCommand(Verify);
        OpenFileCommand = new RelayCommand(OpenFile);
        UseThisComputerCommand = new RelayCommand(UseThisComputer);
        ClearCommand = new RelayCommand(Clear);
        ProceedCommand = new RelayCommand(
            () =>
            {
                if (_parsed is not null) ProceedRequested?.Invoke(_parsed.InstallationId);
            },
            () => _parsed is not null);
    }

    public RelayCommand VerifyCommand { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand UseThisComputerCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand ProceedCommand { get; }

    public string RequestText
    {
        get => _requestText;
        set
        {
            if (!Set(ref _requestText, value)) return;
            Parsed = null;
            ClearStatus();
        }
    }

    public ParsedRequest? Parsed
    {
        get => _parsed;
        private set
        {
            if (!Set(ref _parsed, value)) return;
            OnPropertyChanged(nameof(HasParsed));
            OnPropertyChanged(nameof(ParsedInstallationId));
            OnPropertyChanged(nameof(ParsedRequestCode));
        }
    }

    public bool HasParsed => _parsed is not null;
    public string ParsedInstallationId => _parsed?.InstallationId ?? "";
    public string ParsedRequestCode => _parsed?.RequestCode ?? "";

    public string Caution =>
        "What this shows: the code is intact and encodes the technical identity of one installation. "
        + "What it does not show: who the customer is, whether they paid, or that the computer is really theirs. "
        + "Confirm that yourself before you issue a license.";

    private void Verify()
    {
        try
        {
            Apply(RequestService.Parse(_requestText), "The request code is well-formed and its checksum is correct.", StatusKind.Success);
        }
        catch (LicenseToolException ex)
        {
            Parsed = null;
            SetStatus(StatusKind.Error, ex.Message);
        }
    }

    private void OpenFile()
    {
        var path = _dialogs.PickOpenPath("Open request file", "Request files (*.txt;*.req)|*.txt;*.req|All files (*.*)|*.*");
        if (path is null) return;
        try
        {
            var parsed = RequestService.ParseFile(path);
            RequestText = parsed.RequestCode;
            Apply(parsed, "Request file read. The code is well-formed and its checksum is correct.", StatusKind.Success);
        }
        catch (Exception ex) when (ex is LicenseToolException or IOException or UnauthorizedAccessException)
        {
            Parsed = null;
            SetStatus(StatusKind.Error, Describe(ex));
        }
    }

    private void UseThisComputer()
    {
        var id = new MachineInstallationIdentity().InstallationId;
        if (id is null)
        {
            Parsed = null;
            SetStatus(StatusKind.Error, "This computer's identity could not be read.");
            return;
        }
        var code = InstallationIds.ToRequestCode(id);
        RequestText = code;
        Apply(RequestService.Parse(code),
            "Using this computer's own request code, for testing only. Real customers send their own code.",
            StatusKind.Info);
    }

    private void Clear()
    {
        RequestText = "";
        Parsed = null;
        ClearStatus();
    }

    private void Apply(ParsedRequest request, string message, StatusKind kind)
    {
        Parsed = request;
        SetStatus(kind, message);
    }
}
