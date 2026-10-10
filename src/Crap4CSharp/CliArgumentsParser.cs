namespace Crap4CSharp;

internal static class CliArgumentsParser
{
    public static CliArguments Parse(string[] args)
    {
        if (args is [])
        {
            return new CliArguments(CliMode.AllSrc, []);
        }

        if (ContainsFlag(args, "--help"))
        {
            return new CliArguments(CliMode.Help, []);
        }

        bool changed = ContainsFlag(args, "--changed");
        List<string> values = NonFlagArgs(args);
        EnsureChangedIsNotCombined(changed, values);
        if (changed)
        {
            return new CliArguments(CliMode.ChangedSrc, []);
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
        List<string> values = [];
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
        if (changed && values is not [])
        {
            throw new InvalidOperationException("--changed cannot be combined with file arguments");
        }
    }
}
