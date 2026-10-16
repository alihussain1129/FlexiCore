using System.Text;

namespace FlexiCore.LicenseTool;

public interface IToolConsole
{
    TextWriter Out { get; }
    TextWriter Error { get; }

    /// <summary>Reads a secret without echo. Returns null when it cannot be typed interactively.</summary>
    string? ReadPassphrase(string prompt);
}

public sealed class SystemToolConsole : IToolConsole
{
    public TextWriter Out => Console.Out;
    public TextWriter Error => Console.Error;

    public string? ReadPassphrase(string prompt)
    {
        if (Console.IsInputRedirected) return null;

        Console.Error.Write(prompt);
        var text = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (text.Length > 0) text.Length--;
                continue;
            }
            if (!char.IsControl(key.KeyChar)) text.Append(key.KeyChar);
        }
        Console.Error.WriteLine();
        return text.ToString();
    }
}