using System.Text;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Services;

public static class SafeFile
{
    /// <summary>Creates a new file. Never replaces an existing one. A partly written file is removed again.</summary>
    public static void WriteNew(string path, byte[] bytes)
    {
        OutputPathGuard.EnsureAbsolute(path, "The destination path");
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            throw new LicenseToolException("The destination folder does not exist.");
        if (File.Exists(full))
            throw new LicenseToolException("A file already exists at that location. Nothing was overwritten. Choose another name.");

        var created = false;
        try
        {
            using var stream = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created = true;
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (created)
            {
                try { File.Delete(full); } catch (Exception) { }
            }
            throw new LicenseToolException(
                ex is UnauthorizedAccessException
                    ? "Access was denied while writing the file. Check the folder permissions."
                    : "The file could not be written: " + ex.Message,
                2);
        }
    }

    public static string ReadTextLimited(string path, long maxBytes)
    {
        var full = Path.GetFullPath(path);
        var info = new FileInfo(full);
        if (!info.Exists) throw new LicenseToolException("The file was not found.");
        if (info.Length > maxBytes)
            throw new LicenseToolException("The file is too large to be a request file (limit " + maxBytes + " bytes).");
        return File.ReadAllText(full, Encoding.UTF8);
    }
}
