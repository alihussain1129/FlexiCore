using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.ViewModels;

public enum StatusKind { None, Info, Success, Error }

public sealed record DetailRow(string Label, string Value);

public abstract class PageViewModel : ObservableObject
{
    private string _statusText = "";
    private StatusKind _statusKind = StatusKind.None;
    private bool _isBusy;

    protected PageViewModel(string title, string subtitle, string glyph)
    {
        Title = title;
        Subtitle = subtitle;
        Glyph = glyph;
    }

    public string Title { get; }
    public string Subtitle { get; }
    public string Glyph { get; }

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public string StatusKindName => _statusKind.ToString();
    public bool HasStatus => _statusKind != StatusKind.None;

    public bool IsBusy
    {
        get => _isBusy;
        protected set
        {
            if (Set(ref _isBusy, value)) OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    public bool IsNotBusy => !_isBusy;

    protected void SetStatus(StatusKind kind, string text)
    {
        _statusKind = kind;
        OnPropertyChanged(nameof(StatusKindName));
        OnPropertyChanged(nameof(HasStatus));
        StatusText = text;
    }

    protected void ClearStatus() => SetStatus(StatusKind.None, "");

    /// <summary>Safe to show in the UI: expected failures show their message, unexpected ones only their type.</summary>
    protected static string Describe(Exception ex) => ex switch
    {
        LicenseToolException lt => lt.Message,
        UnauthorizedAccessException => "Access was denied. Check the folder permissions.",
        IOException io => "A file operation failed: " + io.Message,
        _ => "Unexpected error (" + ex.GetType().Name + ").",
    };

    /// <summary>Called whenever the page is opened, so data is always fresh.</summary>
    public virtual Task OnShownAsync() => Task.CompletedTask;
}
