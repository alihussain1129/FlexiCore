namespace FlexiCore.ActivationEngine.Services;

/// <summary>
/// Used by the real Issue page only. It answers the "where to save" question automatically
/// (Documents\FlexiCore Licenses\customer\file.lic) and skips the extra confirmation, because the Issue button
/// is the confirmation. Everything else is passed to the real dialogs. The signing service still refuses to
/// overwrite an existing file.
/// </summary>
public sealed class AutoSaveDialogService : IDialogService
{
    private readonly IDialogService _inner;
    private readonly string _root;

    public AutoSaveDialogService(IDialogService inner, string root)
    {
        _inner = inner;
        _root = root;
    }

    public static string DefaultRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FlexiCore Licenses");

    public string? PickSavePath(string title, string defaultFileName, string filter)
    {
        var name = Path.GetFileName(defaultFileName);
        var stem = Path.GetFileNameWithoutExtension(name);
        var cut = stem.LastIndexOf('_');
        var folderName = cut > 0 ? stem.Substring(0, cut) : stem;
        if (folderName.Length == 0) folderName = "customer";
        var folder = Path.Combine(_root, folderName);
        try { Directory.CreateDirectory(folder); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* the signing step reports a missing folder */ }
        return Path.Combine(folder, name);
    }

    public string? PickOpenPath(string title, string filter) => _inner.PickOpenPath(title, filter);
    public bool Confirm(string title, string message) => true;
    public void Reveal(string filePath) => _inner.Reveal(filePath);
    public void OpenFolder(string folderPath) => _inner.OpenFolder(folderPath);
}