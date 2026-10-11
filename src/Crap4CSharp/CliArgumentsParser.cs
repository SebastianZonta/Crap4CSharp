namespace Crap4CSharp;

internal static class CliArgumentsParser
{
    public const double DefaultThreshold = ToolConstants.DefaultCrapThreshold;

    public static CliArguments Parse(string[] args)
    {
        string? projectPath = null;
        string? outputPath = null;
        var threshold = DefaultThreshold;
        var format = "text";
        int? top = null;
        var minComplexity = 1;
        var noCoverage = false;
        var changed = false;
        var help = false;
        var version = false;
        List<string>? values = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i].AsSpan();
            if (arg.SequenceEqual("--"))
            {
                for (var j = i + 1; j < args.Length; j++)
                {
                    values ??= [];
                    values.Add(args[j]);
                }
                break;
            }
            if (TrySplitFlagValue(arg, out var name, out var inlineValue, out var hasInline))
            {
                if (IsFlag(name, "--help") || IsFlag(name, "-h") || IsFlag(name, "-?"))
                {
                    RejectValue(name, hasInline);
                    help = true;
                }
                else if (IsFlag(name, "--version"))
                {
                    RejectValue(name, hasInline);
                    version = true;
                }
                else if (IsFlag(name, "--changed"))
                {
                    RejectValue(name, hasInline);
                    changed = true;
                }
                else if (IsFlag(name, "--no-coverage"))
                {
                    RejectValue(name, hasInline);
                    noCoverage = true;
                }
                else if (IsFlag(name, "--path"))
                {
                    projectPath = TakeValue(args, ref i, inlineValue, hasInline, name);
                }
                else if (IsFlag(name, "--output") || IsFlag(name, "-o"))
                {
                    outputPath = TakeValue(args, ref i, inlineValue, hasInline, name);
                }
                else if (IsFlag(name, "--threshold") || IsFlag(name, "-t"))
                {
                    threshold = ParseThreshold(TakeValueSpan(args, ref i, inlineValue, hasInline, name));
                }
                else if (IsFlag(name, "--format") || IsFlag(name, "-f"))
                {
                    format = ParseFormat(TakeValueSpan(args, ref i, inlineValue, hasInline, name));
                }
                else if (IsFlag(name, "--top"))
                {
                    top = ParsePositiveInt(TakeValueSpan(args, ref i, inlineValue, hasInline, name), name);
                }
                else if (IsFlag(name, "--min-cc"))
                {
                    minComplexity = ParsePositiveInt(TakeValueSpan(args, ref i, inlineValue, hasInline, name), name);
                }
                else
                {
                    throw new InvalidOperationException($"Unknown option '{new string(name)}'. See --help.");
                }
            }
            else if ((arg.StartsWith("--", StringComparison.Ordinal) && arg.Length > 2)
                || (arg.Length == 2 && arg[0] == '-'))
            {
                // Long flag without '=', or short flag.
                string flag = new(arg);
                switch (flag)
                {
                    case "--help":
                    case "-h":
                        help = true;
                        break;
                    case "--version":
                        version = true;
                        break;
                    case "--changed":
                        changed = true;
                        break;
                    case "--no-coverage":
                        noCoverage = true;
                        break;
                    case "--path":
                    case "--output":
                    case "-o":
                    case "--threshold":
                    case "-t":
                    case "--format":
                    case "-f":
                    case "--top":
                    case "--min-cc":
                        throw new InvalidOperationException($"Missing value for '{flag}'. Use '{flag}=<value>' or '{flag} <value>'.");
                    default:
                        throw new InvalidOperationException($"Unknown option '{flag}'. See --help.");
                }
            }
            else if (arg.SequenceEqual("-?") || arg.SequenceEqual("/?"))
            {
                help = true;
            }
            else
            {
                values ??= [];
                values.Add(args[i]);
            }
        }

        if (help)
        {
            return new CliArguments(CliMode.Help, []);
        }
        if (version)
        {
            return new CliArguments(CliMode.Version, []);
        }
        if (changed)
        {
            if (values is not null && values.Count != 0)
            {
                throw new InvalidOperationException("--changed cannot be combined with file arguments");
            }
            return new CliArguments(
                CliMode.ChangedSrc, [], projectPath, outputPath,
                threshold, format, top, minComplexity, noCoverage);
        }
        if (values is null || values.Count == 0)
        {
            return new CliArguments(
                CliMode.AllSrc, [], projectPath, outputPath,
                threshold, format, top, minComplexity, noCoverage);
        }
        return new CliArguments(
            CliMode.ExplicitFiles, values.ToArray(), projectPath, outputPath,
            threshold, format, top, minComplexity, noCoverage);
    }

    private static bool IsFlag(ReadOnlySpan<char> name, ReadOnlySpan<char> flag) =>
        name.SequenceEqual(flag);

    private static bool TrySplitFlagValue(
        ReadOnlySpan<char> arg, out ReadOnlySpan<char> name, out ReadOnlySpan<char> value, out bool hasValue)
    {
        var eq = arg.IndexOf('=');
        if (eq > 0 && arg[0] == '-')
        {
            name = arg[..eq];
            value = arg[(eq + 1)..];
            hasValue = true;
            return true;
        }
        name = arg;
        value = default;
        hasValue = false;
        return arg.Length > 0 && arg[0] == '-';
    }

    private static void RejectValue(ReadOnlySpan<char> name, bool hasValue)
    {
        if (hasValue)
        {
            throw new InvalidOperationException($"Option '{new string(name)}' does not take a value.");
        }
    }

    private static string TakeValue(
        string[] args, ref int i, ReadOnlySpan<char> inlineValue, bool hasInline, ReadOnlySpan<char> name)
    {
        if (hasInline)
        {
            if (inlineValue.Length == 0)
            {
                throw new InvalidOperationException($"Missing value for '{new string(name)}'.");
            }
            return new string(inlineValue);
        }
        if (i + 1 >= args.Length)
        {
            string flag = new(name);
            throw new InvalidOperationException($"Missing value for '{flag}'. Use '{flag}=<value>' or '{flag} <value>'.");
        }
        i++;
        return args[i];
    }

    private static ReadOnlySpan<char> TakeValueSpan(
        string[] args, ref int i, ReadOnlySpan<char> inlineValue, bool hasInline, ReadOnlySpan<char> name)
    {
        if (hasInline)
        {
            if (inlineValue.Length == 0)
            {
                throw new InvalidOperationException($"Missing value for '{new string(name)}'.");
            }
            return inlineValue;
        }
        if (i + 1 >= args.Length)
        {
            string flag = new(name);
            throw new InvalidOperationException($"Missing value for '{flag}'. Use '{flag}=<value>' or '{flag} <value>'.");
        }
        i++;
        return args[i].AsSpan();
    }

    private static double ParseThreshold(ReadOnlySpan<char> raw)
    {
        if (!double.TryParse(
                raw,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value) || double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            throw new InvalidOperationException($"Invalid --threshold '{new string(raw)}'. Expected a positive number (default {DefaultThreshold:F1}).");
        }
        return value;
    }

    private static string ParseFormat(ReadOnlySpan<char> raw)
    {
        var normalized = raw.Trim();
        if (normalized.Equals("text", StringComparison.OrdinalIgnoreCase))
        {
            return "text";
        }
        if (normalized.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return "json";
        }
        if (normalized.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            return "csv";
        }
        throw new InvalidOperationException($"Invalid --format '{new string(raw)}'. Expected text, json, or csv.");
    }

    private static int ParsePositiveInt(ReadOnlySpan<char> raw, ReadOnlySpan<char> name)
    {
        if (!int.TryParse(
                raw,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value) || value <= 0)
        {
            throw new InvalidOperationException($"Invalid {new string(name)} '{new string(raw)}'. Expected a positive integer.");
        }
        return value;
    }
}