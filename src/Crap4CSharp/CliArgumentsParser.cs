namespace Crap4CSharp;

public static class CliArgumentsParser
{
    public static CliArguments Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliArguments(CliMode.AllSrc, Array.Empty<string>());
        }

        if (ContainsFlag(args, "--help"))
        {
            return new CliArguments(CliMode.Help, Array.Empty<string>());
        }

        bool changed = ContainsFlag(args, "--changed");
        List<string> values = NonFlagArgs(args);
        EnsureChangedIsNotCombined(changed, values);
        if (changed)
        {
            return new CliArguments(CliMode.ChangedSrc, Array.Empty<string>());
        }
        return new CliArguments(CliMode.ExplicitFiles, values.AsReadOnly());
    }

    private static bool ContainsFlag(string[] args, string flag)
    {
        foreach (string arg in args)
        {
            if (flag.Equals(arg, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static List<string> NonFlagArgs(string[] args)
    {
        var values = new List<string>();
        foreach (string arg in args)
        {
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }
            values.Add(arg);
        }
        return values;
    }

    private static void EnsureChangedIsNotCombined(bool changed, List<string> values)
    {
        if (changed && values.Count > 0)
        {
            throw new InvalidOperationException("--changed cannot be combined with file arguments");
        }
    }
}
