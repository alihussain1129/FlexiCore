using Android.App;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FlexiCore.ActivationEngine.Services;

namespace FlexiCore.ActivationApp;

internal sealed class AndroidDialogService(
    Func<TopLevel?> topLevel,
    OwnerPaths paths) : IActivationDialogs
{
    private const long MaximumImportedFileBytes = 50L * 1024 * 1024;
    private readonly Dictionary<string, IStorageFile> _pendingExports = new(StringComparer.Ordinal);

    public string? PickSavePath(string title, string defaultFileName, string filter) => null;
    public string? PickOpenPath(string title, string filter) => null;
    public bool Confirm(string title, string message) => false;
    public void Reveal(string filePath) => _ = ShowMessageAsync("Owner-private file", "Files are stored in the app-private data area. Use an export action to copy a file to shared storage.");
    public void OpenFolder(string folderPath) => _ = ShowMessageAsync("Owner-private storage", "Android keeps owner data inside the application sandbox. Use the export actions to create external backups.");

    public async Task<string?> PickSavePathAsync(string title, string defaultFileName, string filter)
    {
        try
        {
            var provider = RequireTopLevel().StorageProvider;
            if (!provider.CanSave)
            {
                await ShowErrorAsync("Save is unavailable", "Android did not provide a document save picker.");
                return null;
            }

            var destination = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = SafeName(defaultFileName),
                FileTypeChoices = FileTypes(filter)
            });
            if (destination is null) return null;

            var stageDirectory = Path.Combine(paths.Root, "staging", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stageDirectory);
            var stagePath = Path.Combine(stageDirectory, SafeName(defaultFileName));
            if (!_pendingExports.TryAdd(stagePath, destination))
                throw new IOException("Could not reserve a private staging file for the selected document.");
            return stagePath;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not open save picker", ex.Message);
            return null;
        }
    }

    public async Task<string?> PickOpenPathAsync(string title, string filter)
    {
        try
        {
            var provider = RequireTopLevel().StorageProvider;
            if (!provider.CanOpen)
            {
                await ShowErrorAsync("Open is unavailable", "Android did not provide a document picker.");
                return null;
            }

            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = FileTypes(filter)
            });
            if (files.Count == 0) return null;

            var importDirectory = Path.Combine(paths.Root, "imports", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(importDirectory);
            var destination = Path.Combine(importDirectory, SafeName(files[0].Name));
            await using var input = await files[0].OpenReadAsync();
            await CopyImportedDocumentAsync(input, destination);
            return destination;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not import document", ex.Message);
            return null;
        }
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var activity = RequireActivity();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        activity.RunOnUiThread(() =>
        {
            var builder = new AlertDialog.Builder(activity);
            builder.SetTitle(title);
            builder.SetMessage(message);
            builder.SetCancelable(false);
            builder.SetNegativeButton("Cancel", (_, _) => completion.TrySetResult(false));
            builder.SetPositiveButton("Continue", (_, _) => completion.TrySetResult(true));
            builder.Show();
        });
        return await completion.Task;
    }

    public Task ShowErrorAsync(string title, string message) => ShowMessageAsync(title, message);

    public async Task ShowMessageAsync(string title, string message)
    {
        var activity = RequireActivity();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        activity.RunOnUiThread(() =>
        {
            var builder = new AlertDialog.Builder(activity);
            builder.SetTitle(title);
            builder.SetMessage(message);
            builder.SetCancelable(false);
            builder.SetPositiveButton("Close", (_, _) => completion.TrySetResult());
            builder.Show();
        });
        await completion.Task;
    }

    public async Task CompleteSaveAsync(string localPath)
    {
        var fullPath = Path.GetFullPath(localPath);
        if (!_pendingExports.Remove(fullPath, out var destination)) return;

        try
        {
            await using var input = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var output = await destination.OpenWriteAsync();
            await input.CopyToAsync(output);
            await output.FlushAsync();
        }
        catch
        {
            _pendingExports[fullPath] = destination;
            throw;
        }
    }

    public async Task<bool> ExportCreatedFileAsync(string localPath, string fileName, string filter)
    {
        var destination = await PickSavePathAsync("Export file", fileName, filter);
        if (destination is null) return false;
        SafeFile.CopyNew(localPath, destination);
        await CompleteSaveAsync(destination);
        return true;
    }

    private TopLevel RequireTopLevel() =>
        topLevel() ?? throw new InvalidOperationException("The Android storage provider is unavailable.");

    private static MainActivity RequireActivity() =>
        MainActivity.Current ?? throw new InvalidOperationException("The Android activity is not available.");

    private static async Task CopyImportedDocumentAsync(Stream source, string destination)
    {
        var created = false;
        try
        {
            await using var output = new FileStream(
                destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created = true;
            var buffer = new byte[81920];
            long total = 0;
            while (true)
            {
                var count = await source.ReadAsync(buffer);
                if (count == 0) break;
                total += count;
                if (total > MaximumImportedFileBytes)
                    throw new InvalidDataException("Imported files are limited to 50 MB.");
                await output.WriteAsync(buffer.AsMemory(0, count));
            }
            await output.FlushAsync();
        }
        catch
        {
            if (created)
            {
                try { File.Delete(destination); } catch (IOException) { }
            }
            throw;
        }
    }

    private static IReadOnlyList<FilePickerFileType> FileTypes(string filter)
    {
        var parts = filter.Split('|', StringSplitOptions.TrimEntries);
        var result = new List<FilePickerFileType>();
        for (var index = 0; index + 1 < parts.Length; index += 2)
        {
            var patterns = parts[index + 1].Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (patterns.Length > 0) result.Add(new FilePickerFileType(parts[index]) { Patterns = patterns });
        }
        return result;
    }

    private static string SafeName(string name)
    {
        var fileName = Path.GetFileName(name);
        var sanitized = new string(fileName
            .Select(c => char.IsControl(c) || Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "export.bin" : sanitized;
    }
}
