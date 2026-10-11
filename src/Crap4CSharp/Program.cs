namespace Crap4CSharp;

internal static class Program
{
    public static int Main(string[] args)
    {
        var projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler? handler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        Console.CancelKeyPress += handler;
        try
        {
            return RunAsync(args, projectRoot, Console.Out, Console.Error, cts.Token)
                .GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    internal static int Run(string[] args, string projectRoot, TextWriter output, TextWriter err) =>
        Run(args, projectRoot, output, err, new CoverageRunner(new ProcessCommandExecutor()));

    internal static int Run(
        string[] args, string projectRoot, TextWriter output, TextWriter err, CoverageRunner coverageRunner) =>
        RunAsync(args, projectRoot, output, err, coverageRunner).GetAwaiter().GetResult();

    internal static Task<int> RunAsync(
        string[] args, string projectRoot, TextWriter output, TextWriter err,
        CancellationToken cancellationToken = default) =>
        RunAsync(args, projectRoot, output, err, new CoverageRunner(new ProcessCommandExecutor()), cancellationToken);

    internal static Task<int> RunAsync(
        string[] args, string projectRoot, TextWriter output, TextWriter err,
        CoverageRunner coverageRunner, CancellationToken cancellationToken = default) =>
        new CliApplication(projectRoot, output, err, coverageRunner).ExecuteAsync(args, cancellationToken);

    internal static string Usage() => UsageText;

    internal static string Version()
    {
        var v = typeof(Program).Assembly.GetName().Version;
        return "crap4csharp " + (v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}");
    }

    private const string UsageText =
        """
        Usage:
          crap4csharp [options] [--] [<path...>]

        Modes (positional, backward compatible):
          (no args)            Analyze all C# files under <root>/src/ (whole tree if no src/)
          --changed            Analyze changed C# files under <root>/src/ (git status)
          <path...>            Analyze files; directories, .sln/.slnx and .csproj expand to all C# (skips bin/obj)

        Options:
          --path <dir|sln|slnx|csproj>  Workspace root (default: current directory).
                                   A .sln/.slnx/.csproj file resolves to its directory.
          --output, -o <file>      Write the report to <file> too (dirs created).
          --threshold, -t <n>      CRAP gate (default 8.0). Fails with exit 2 when max > n.
          --format, -f <fmt>       Report format: text (default), json, csv.
          --top <n>                Show only the n worst methods.
          --min-cc <n>             Hide methods with complexity below n (default 1).
          --no-coverage            Skip 'dotnet test' coverage run; reuse existing XML.
          --help, -h               Print this help message.
          --version                Print version.
        """;

    internal static double MaxCrap(IEnumerable<MethodMetrics> metrics) =>
        CrapScore.MaxCrap(metrics);
}