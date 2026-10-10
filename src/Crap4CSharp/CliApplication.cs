namespace Crap4CSharp;

internal sealed class CliApplication
{
    private static EnumerationOptions NewCoberturaEnumeration() => new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
    };

    private readonly string _projectRoot;
    private readonly string _projectPrefix;
    private readonly TextWriter _out;
    private readonly TextWriter _err;
    private readonly CoverageRunner _coverageRunner;

    internal CliApplication(
        string projectRoot, TextWriter output, TextWriter err, CoverageRunner coverageRunner)
    {
        _projectRoot = Path.GetFullPath(projectRoot);
        _projectPrefix = _projectRoot + Path.DirectorySeparatorChar;
        _out = output;
        _err = err;
        _coverageRunner = coverageRunner;
    }

    internal int Execute(string[] args)
    {
        ParseOutcome parse = ParseArguments(args);
        if (parse.ExitCode >= 0)
        {
            return parse.ExitCode;
        }
        CliArguments parsed = parse.Arguments!;
        IReadOnlyList<string> filesToAnalyze = FilesForMode(parsed);
        if (filesToAnalyze.Count == 0)
        {
            _out.WriteLine("No C# files to analyze.");
            return 0;
        }

        List<MethodMetrics> metrics = AnalyzeByModule(filesToAnalyze);
        metrics.Sort(static (a, b) => CompareByCrapDesc(a, b));
        _out.Write(ReportFormatter.Format(metrics));

        double max = Program.MaxCrap(metrics);
        if (ThresholdExceeded(max))
        {
            _err.WriteLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "CRAP threshold exceeded: {0:F1} > 8.0", max));
            return 2;
        }
        return 0;
    }

    private List<MethodMetrics> AnalyzeByModule(IReadOnlyList<string> filesToAnalyze)
    {
        List<MethodMetrics> metrics = [];
        foreach (var entry in GroupByModuleRoot(filesToAnalyze))
        {
            string moduleRoot = entry.Key;
            _coverageRunner.GenerateCoverage(moduleRoot);
            string? coberturaXml = FindCoberturaXml(moduleRoot);
            if (coberturaXml is null)
            {
                _err.WriteLine("Warning: Cobertura XML not found at "
                    + Path.Combine(moduleRoot, "TestResults") + ". Coverage will be N/A.");
            }
            metrics.AddRange(CrapAnalyzer.Analyze(moduleRoot, entry.Value, coberturaXml));
        }
        return metrics;
    }

    private static string? FindCoberturaXml(string moduleRoot)
    {
        string testResultsDir = Path.Combine(moduleRoot, "TestResults");
        if (!Directory.Exists(testResultsDir))
        {
            return null;
        }
        string? min = null;
        foreach (string candidate in Directory.EnumerateFiles(
            testResultsDir, "coverage.cobertura.xml", NewCoberturaEnumeration()))
        {
            if (min is null || string.Compare(candidate, min, StringComparison.Ordinal) < 0)
            {
                min = candidate;
            }
        }
        return min;
    }

    private static int CompareByCrapDesc(MethodMetrics a, MethodMetrics b) =>
        (a.CrapScore, b.CrapScore) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            (var x, var y) => y.Value.CompareTo(x.Value),
        };

    internal static bool ThresholdExceeded(double max) => max.CompareTo(8.0) > 0;

    private ParseOutcome ParseArguments(string[] args)
    {
        try
        {
            CliArguments parsed = CliArgumentsParser.Parse(args);
            if (parsed.Mode == CliMode.Help)
            {
                _out.WriteLine(Program.Usage());
                return ParseOutcome.Exit(0);
            }
            return ParseOutcome.Ok(parsed);
        }
        catch (InvalidOperationException ex)
        {
            _err.WriteLine(ex.Message);
            _out.WriteLine(Program.Usage());
            return ParseOutcome.Exit(1);
        }
    }

    private IReadOnlyList<string> FilesForMode(CliArguments parsed) => parsed.Mode switch
    {
        CliMode.AllSrc => SourceFileFinder.FindAllCsFilesUnderSrc(_projectRoot),
        CliMode.ChangedSrc => ChangedFileDetector.ChangedCsFilesUnderSrc(_projectRoot),
        CliMode.ExplicitFiles => ExplicitFiles(parsed.FileArgs),
        _ => [],
    };

    private string[] ExplicitFiles(IReadOnlyList<string> args)
    {
        var files = new HashSet<string>(StringComparer.Ordinal);
        foreach (string arg in args)
        {
            string path = Path.GetFullPath(Path.Combine(_projectRoot, arg));
            if (Directory.Exists(path))
            {
                foreach (string file in SourceFileFinder.FindAllCsFilesUnderSrc(path))
                {
                    files.Add(file);
                }
            }
            else
            {
                files.Add(path);
            }
        }
        string[] result = [.. files];
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    internal static string ModuleRootFor(string workspaceRoot, string file)
    {
        string normalized = Path.GetFullPath(workspaceRoot);
        return ModuleRootForNormalized(normalized, normalized + Path.DirectorySeparatorChar, file);
    }

    private string ModuleRootForCurrent(string file) =>
        ModuleRootForNormalized(_projectRoot, _projectPrefix, file);

    private static string ModuleRootForNormalized(string normalizedRoot, string rootPrefix, string file)
    {
        string? current = Directory.Exists(file)
            ? Path.GetFullPath(file)
            : Path.GetDirectoryName(Path.GetFullPath(file));
        while (current is not null && IsWithinOrEqual(current, normalizedRoot, rootPrefix))
        {
            if (HasCsproj(current))
            {
                return current;
            }
            current = Path.GetDirectoryName(current);
        }
        return normalizedRoot;
    }

    private static bool HasCsproj(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*.csproj").Any();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
        {
            return false;
        }
    }

    private Dictionary<string, List<string>> GroupByModuleRoot(IReadOnlyList<string> filesToAnalyze)
    {
        var grouped = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string file in filesToAnalyze)
        {
            string moduleRoot = ModuleRootForCurrent(file);
            if (!grouped.TryGetValue(moduleRoot, out List<string>? list))
            {
                list = [];
                grouped[moduleRoot] = list;
            }
            list.Add(file);
        }
        return grouped;
    }

    private static bool IsWithinOrEqual(string candidate, string root, string rootPrefix) =>
        candidate.Equals(root, StringComparison.Ordinal) ||
        candidate.StartsWith(rootPrefix, StringComparison.Ordinal);

    private sealed class ParseOutcome
    {
        public CliArguments? Arguments { get; }
        public int ExitCode { get; }

        private ParseOutcome(CliArguments? arguments, int exitCode)
        {
            Arguments = arguments;
            ExitCode = exitCode;
        }

        public static ParseOutcome Ok(CliArguments arguments) => new(arguments, -1);
        public static ParseOutcome Exit(int code) => new(null, code);
    }
}
