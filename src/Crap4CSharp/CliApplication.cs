namespace Crap4CSharp;

internal sealed class CliApplication
{
    internal const double DefaultThreshold = ToolConstants.DefaultCrapThreshold;

    private readonly string _projectRoot;
    private readonly TextWriter _out;
    private readonly TextWriter _err;
    private readonly CoverageRunner _coverageRunner;

    internal CliApplication(
        string projectRoot, TextWriter output, TextWriter err, CoverageRunner coverageRunner)
    {
        _projectRoot = Path.GetFullPath(projectRoot);
        _out = output;
        _err = err;
        _coverageRunner = coverageRunner;
    }

    internal int Execute(string[] args) =>
        ExecuteAsync(args).GetAwaiter().GetResult();

    internal async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ParseOutcome parse = ParseArguments(args);
        if (parse.ExitCode >= 0)
        {
            return parse.ExitCode;
        }
        CliArguments parsed = parse.Arguments!;
        if (parsed.Mode == CliMode.Version)
        {
            await _out.WriteLineAsync(Program.Version()).ConfigureAwait(false);
            return 0;
        }

        string effectiveRoot = ResolveRoot(parsed.ProjectPath);
        try
        {
            return await ExecuteOnRootAsync(parsed, effectiveRoot, cancellationToken).ConfigureAwait(false);
        }
        catch (DirectoryNotFoundException ex)
        {
            await _err.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return 1;
        }
    }

    private async Task<int> ExecuteOnRootAsync(
        CliArguments parsed, string effectiveRoot, CancellationToken cancellationToken)
    {
        string effectivePrefix = effectiveRoot + Path.DirectorySeparatorChar;
        IReadOnlyList<string> filesToAnalyze = await FilesForModeAsync(parsed, effectiveRoot, cancellationToken).ConfigureAwait(false);
        if (filesToAnalyze.Count == 0)
        {
            await _out.WriteLineAsync(ToolConstants.NoFilesMessage).ConfigureAwait(false);
            return 0;
        }

        List<MethodMetrics> metrics = await AnalyzeByModuleAsync(
            filesToAnalyze, effectiveRoot, effectivePrefix, parsed.NoCoverage, cancellationToken).ConfigureAwait(false);
        metrics.Sort(CrapScore.CompareByCrapDesc);
        if (parsed.MinComplexity > 1)
        {
            metrics.RemoveAll(m => m.Complexity < parsed.MinComplexity);
        }
        if (parsed.Top.HasValue && metrics.Count > parsed.Top.Value)
        {
            metrics.RemoveRange(parsed.Top.Value, metrics.Count - parsed.Top.Value);
        }
        string report = ReportFormatter.FormatAs(metrics, parsed.Format);
        await _out.WriteAsync(report).ConfigureAwait(false);
        if (parsed.OutputPath is not null)
        {
            await WriteReportFileAsync(parsed.OutputPath, report, cancellationToken).ConfigureAwait(false);
        }

        double max = metrics.MaxCrap();
        if (ThresholdExceeded(max, parsed.Threshold))
        {
            await _err.WriteLineAsync(ToolConstants.ThresholdExceededMessage(max, parsed.Threshold)).ConfigureAwait(false);
            return 2;
        }
        return 0;
    }

    private async Task WriteReportFileAsync(string outputPath, string report, CancellationToken cancellationToken)
    {
        string full = ToolConstants.CombineRooted(_projectRoot, outputPath);
        string? dir = Path.GetDirectoryName(full);
        if (dir is not null)
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllTextAsync(full, report, cancellationToken).ConfigureAwait(false);
    }

    internal string ResolveRoot(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return _projectRoot;
        }
        string combined = ToolConstants.CombineRooted(_projectRoot, projectPath);
        try
        {
            if (File.Exists(combined))
            {
                return Path.GetDirectoryName(combined) ?? _projectRoot;
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return _projectRoot;
        }
        return combined;
    }

    private async Task<List<MethodMetrics>> AnalyzeByModuleAsync(
        IReadOnlyList<string> filesToAnalyze, string effectiveRoot, string effectivePrefix,
        bool noCoverage, CancellationToken cancellationToken)
    {
        List<MethodMetrics> metrics = [];
        foreach (var entry in GroupByModuleRoot(filesToAnalyze, effectiveRoot, effectivePrefix))
        {
            string moduleRoot = entry.Key;
            if (!noCoverage)
            {
                await _coverageRunner.GenerateCoverageAsync(moduleRoot, cancellationToken).ConfigureAwait(false);
            }
            string? coberturaXml = FindCoberturaXml(moduleRoot);
            if (coberturaXml is null)
            {
                await _err.WriteLineAsync(ToolConstants.CoverageMissingWarning(moduleRoot)).ConfigureAwait(false);
            }
            metrics.AddRange(CrapAnalyzer.Analyze(moduleRoot, entry.Value, coberturaXml));
        }
        return metrics;
    }

    private static string? FindCoberturaXml(string moduleRoot)
    {
        string testResultsDir = Path.Combine(moduleRoot, ToolConstants.TestResultsDirName);
        if (!Directory.Exists(testResultsDir))
        {
            return null;
        }
        string? min = null;
        foreach (string candidate in Directory.EnumerateFiles(
            testResultsDir, ToolConstants.CoberturaFileName, FileSystemHelpers.SharedEnumeration()))
        {
            if (min is null || string.Compare(candidate, min, StringComparison.Ordinal) < 0)
            {
                min = candidate;
            }
        }
        return min;
    }

    internal static bool ThresholdExceeded(double max) => ThresholdExceeded(max, DefaultThreshold);

    internal static bool ThresholdExceeded(double max, double threshold) => max.CompareTo(threshold) > 0;

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

    private static async Task<IReadOnlyList<string>> FilesForModeAsync(
        CliArguments parsed, string effectiveRoot, CancellationToken cancellationToken) => parsed.Mode switch
    {
        CliMode.AllSrc => SourceFileFinder.FindAllCsFilesUnderSrc(effectiveRoot),
        CliMode.ChangedSrc => await ChangedFileDetector
            .ChangedCsFilesUnderSrcAsync(effectiveRoot, cancellationToken).ConfigureAwait(false),
        CliMode.ExplicitFiles => ExplicitFiles(parsed.FileArgs, effectiveRoot),
        _ => [],
    };

    private static string[] ExplicitFiles(IReadOnlyList<string> args, string effectiveRoot)
    {
        var files = new HashSet<string>(StringComparer.Ordinal);
        foreach (string arg in args)
        {
            string path = ToolConstants.CombineRooted(effectiveRoot, arg);
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

    private static string ModuleRootForNormalized(string normalizedRoot, string rootPrefix, string file)
    {
        string? current = Directory.Exists(file)
            ? Path.GetFullPath(file)
            : Path.GetDirectoryName(Path.GetFullPath(file));
        while (current is not null && current.IsUnderDirectory(normalizedRoot, rootPrefix))
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

    private static Dictionary<string, List<string>> GroupByModuleRoot(
        IReadOnlyList<string> filesToAnalyze, string effectiveRoot, string effectivePrefix)
    {
        var grouped = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string file in filesToAnalyze)
        {
            string moduleRoot = ModuleRootForNormalized(effectiveRoot, effectivePrefix, file);
            if (!grouped.TryGetValue(moduleRoot, out List<string>? list))
            {
                list = [];
                grouped[moduleRoot] = list;
            }
            list.Add(file);
        }
        return grouped;
    }

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
