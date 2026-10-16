namespace FlexiCore.LicenseTool;

public sealed class ParsedArgs
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Flags { get; } = new(StringComparer.Ordinal);
    public List<string> Positionals { get; } = [];

    public string Require(string name) =>
        Values.TryGetValue(name, out var value) ? value : throw new LicenseToolException($"Missing --{name}.");

    public string? Optional(string name) => Values.GetValueOrDefault(name);
}

public static class ArgParser
{
    public static ParsedArgs Parse(string[] args, string[] valueOptions, string[] flagOptions)
    {
        var parsed = new ParsedArgs();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                parsed.Positionals.Add(arg);
                continue;
            }

            var name = arg[2..];
            if (flagOptions.Contains(name))
            {
                parsed.Flags.Add(name);
            }
            else if (valueOptions.Contains(name))
            {
                if (i + 1 >= args.Length) throw new LicenseToolException($"--{name} needs a value.");
                if (!parsed.Values.TryAdd(name, args[++i])) throw new LicenseToolException($"--{name} was given twice.");
            }
            else
            {
                throw new LicenseToolException($"Unknown option --{name}.");
            }
        }
        return parsed;
    }
}