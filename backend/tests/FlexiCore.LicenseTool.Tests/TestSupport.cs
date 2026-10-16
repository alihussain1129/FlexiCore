using FlexiCore.LicenseTool;

namespace FlexiCore.LicenseTool.Tests;

internal sealed class FakeConsole : IToolConsole
{
    private readonly Queue<string?> _passphrases;

    public FakeConsole(params string?[] passphrases) => _passphrases = new Queue<string?>(passphrases);

    public StringWriter OutWriter { get; } = new();
    public StringWriter ErrorWriter { get; } = new();
    public TextWriter Out => OutWriter;
    public TextWriter Error => ErrorWriter;
    public string AllOutput => OutWriter + ErrorWriter.ToString();

    public string? ReadPassphrase(string prompt) => _passphrases.Count > 0 ? _passphrases.Dequeue() : null;
}

internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fc-tool-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}