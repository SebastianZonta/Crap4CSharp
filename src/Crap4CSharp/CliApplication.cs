namespace Crap4CSharp;

internal sealed class CliApplication
{
    private readonly string _projectRoot;
    private readonly TextWriter _out;
    private readonly TextWriter _err;
    private readonly CoverageRunner _coverageRunner;

    internal CliApplication(
        string projectRoot, TextWriter output, TextWriter err, CoverageRunner coverageRunner)
    {
        _projectRoot = projectRoot;
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
        metrics.Sort((a, b) =>
            (a.CrapScore, b.CrapScore) switch
            {
                (null, null) => 0,
                (null, _) => 1,
                (_, null) => -1,
                (var x, var y) => y.Value.CompareTo(x.Value),
            });
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
        var metrics = new List<MethodMetrics>();
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
        return Directory.EnumerateFiles(testResultsDir, "coverage.cobertura.xml",
                SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .FirstOrDefault();
    }

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
        _ => Array.Empty<string>(),
    };

    private IReadOnlyList<string> ExplicitFiles(IReadOnlyList<string> args)
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
        return files.OrderBy(p => p, StringComparer.Ordinal).ToList().AsReadOnly();
    }

    internal static string ModuleRootFor(string workspaceRoot, string file)
    {
        string normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        string? current = Directory.Exists(file)
            ? Path.GetFullPath(file)
            : Path.GetDirectoryName(Path.GetFullPath(file));
        while (current is not null && IsWithinOrEqual(current, normalizedWorkspaceRoot))
        {
            if (Directory.EnumerateFiles(current, "*.csproj").Any())
            {
                return current;
            }
            current = Path.GetDirectoryName(current);
        }
        return normalizedWorkspaceRoot;
    }

    private Dictionary<string, List<string>> GroupByModuleRoot(IReadOnlyList<string> filesToAnalyze)
    {
        var grouped = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string file in filesToAnalyze)
        {
            string moduleRoot = ModuleRootFor(_projectRoot, file);
            if (!grouped.TryGetValue(moduleRoot, out List<string>? list))
            {
                list = new List<string>();
                grouped[moduleRoot] = list;
            }
            list.Add(file);
        }
        return grouped;
    }

    private static bool IsWithinOrEqual(string candidate, string root) =>
        candidate.Equals(root, StringComparison.Ordinal) ||
        candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

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
