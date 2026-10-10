namespace Crap4CSharp;

internal static class CliArgumentsParser
{
    public static CliArguments Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliArguments(CliMode.AllSrc, []);
        }

        bool changed = false;
        List<string>? values = null;
        bool help = false;
        foreach (string arg in args)
        {
            if (arg.Equals("--help", StringComparison.Ordinal))
            {
                help = true;
            }
            else if (arg.Equals("--changed", StringComparison.Ordinal))
            {
                changed = true;
            }
            else if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                values ??= [];
                values.Add(arg);
            }
        }

        if (help)
        {
            return new CliArguments(CliMode.Help, []);
        }

        if (changed)
        {
            if (values is not null && values.Count != 0)
            {
                throw new InvalidOperationException("--changed cannot be combined with file arguments");
            }
            return new CliArguments(CliMode.ChangedSrc, []);
        }
        return new CliArguments(CliMode.ExplicitFiles, values?.ToArray() ?? []);
    }
}
