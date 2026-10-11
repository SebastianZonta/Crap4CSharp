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
        var parse = ParseArguments(args);
        if (parse.ExitCode >= 0)
        {
            return parse.ExitCode;
        }
        var parsed = parse.Arguments!;
        if (parsed.Mode == CliMode.Version)
        {
            await _out.WriteLineAsync(Program.Version()).ConfigureAwait(false);
            return 0;
        }

        var effectiveRoot = ResolveRoot(parsed.ProjectPath);
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
        var effectivePrefix = effectiveRoot + Path.DirectorySeparatorChar;
        var filesToAnalyze = await FilesForModeAsync(parsed, effectiveRoot, cancellationToken).ConfigureAwait(false);
        if (filesToAnalyze.Count == 0)
        {
            await _out.WriteLineAsync(ToolConstants.NoFilesMessage).ConfigureAwait(false);
            return 0;
        }

        var metrics = await AnalyzeByModuleAsync(
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
        var report = ReportFormatter.FormatAs(metrics, parsed.Format);
        await _out.WriteAsync(report).ConfigureAwait(false);
        if (parsed.OutputPath is not null)
        {
            await WriteReportFileAsync(parsed.OutputPath, report, cancellationToken).ConfigureAwait(false);
        }

        var max = metrics.MaxCrap();
        if (ThresholdExceeded(max, parsed.Threshold))
        {
            await _err.WriteLineAsync(ToolConstants.ThresholdExceededMessage(max, parsed.Threshold)).ConfigureAwait(false);
            return 2;
        }
        return 0;
    }

    private async Task WriteReportFileAsync(string outputPath, string report, CancellationToken cancellationToken)
    {
        var full = ToolConstants.CombineRooted(_projectRoot, outputPath);
        var dir = Path.GetDirectoryName(full);
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
        var combined = ToolConstants.CombineRooted(_projectRoot, projectPath);
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
            var moduleRoot = entry.Key;
            if (!noCoverage)
            {
                var testTarget = TestTargetFor(effectiveRoot, moduleRoot);
                await _coverageRunner.GenerateCoverageAsync(testTarget, moduleRoot, cancellationToken).ConfigureAwait(false);
            }
            var coberturaXml = FindCoberturaXml(moduleRoot);
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
        var testResultsDir = Path.Combine(moduleRoot, ToolConstants.TestResultsDirName);
        if (!Directory.Exists(testResultsDir))
        {
            return null;
        }
        string? min = null;
        foreach (var candidate in Directory.EnumerateFiles(
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

    // Coverage must run against a testable target: `dotnet test` on a plain
    // library project exits 0 but produces no Cobertura file, so on the
    // standard src/test split the module root alone always yields N/A.
    // When the workspace root holds exactly one solution, test that instead
    // (results still land under the module's TestResults); otherwise keep
    // the module root (e.g. the module is itself a test project).
    internal static string TestTargetFor(string workspaceRoot, string moduleRoot)
    {
        try
        {
            string? single = null;
            var count = 0;
            foreach (var file in Directory.EnumerateFiles(workspaceRoot))
            {
                if (file.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                    || file.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                    single = file;
                }
            }
            if (count == 1 && single is not null)
            {
                return single;
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is DirectoryNotFoundException)
        {
            // Fall through to the module root below.
        }
        return moduleRoot;
    }

    private ParseOutcome ParseArguments(string[] args)
    {
        try
        {
            var parsed = CliArgumentsParser.Parse(args);
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
        foreach (var arg in args)
        {
            var path = ToolConstants.CombineRooted(effectiveRoot, arg);
            if (Directory.Exists(path))
            {
                foreach (var file in SourceFileFinder.FindAllCsFilesUnderSrc(path))
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
        var normalized = Path.GetFullPath(workspaceRoot);
        return ModuleRootForNormalized(normalized, normalized + Path.DirectorySeparatorChar, file);
    }

    private static string ModuleRootForNormalized(string normalizedRoot, string rootPrefix, string file)
    {
        var current = Directory.Exists(file)
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
        foreach (var file in filesToAnalyze)
        {
            var moduleRoot = ModuleRootForNormalized(effectiveRoot, effectivePrefix, file);
            if (!grouped.TryGetValue(moduleRoot, out var list))
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