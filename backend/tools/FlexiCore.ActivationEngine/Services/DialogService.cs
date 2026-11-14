namespace FlexiCore.ActivationEngine.Services;

public interface IDialogService
{
    string? PickSavePath(string title, string defaultFileName, string filter);
    string? PickOpenPath(string title, string filter);
    bool Confirm(string title, string message);
    void Reveal(string filePath);
    void OpenFolder(string folderPath);

    Task<string?> PickSavePathAsync(string title, string defaultFileName, string filter) =>
        Task.FromResult(PickSavePath(title, defaultFileName, filter));

    Task<string?> PickOpenPathAsync(string title, string filter) =>
        Task.FromResult(PickOpenPath(title, filter));

    Task<bool> ConfirmAsync(string title, string message) =>
        Task.FromResult(Confirm(title, message));

    Task CompleteSaveAsync(string localPath) => Task.CompletedTask;

    Task<bool> ExportCreatedFileAsync(string localPath, string fileName, string filter) =>
        Task.FromResult(true);

    async Task<bool> ExportFileAsync(string sourcePath, string fileName, string filter)
    {
        var destination = await PickSavePathAsync("Export file", fileName, filter);
        if (destination is null) return false;
        SafeFile.CopyNew(sourcePath, destination);
        await CompleteSaveAsync(destination);
        return true;
    }
}
