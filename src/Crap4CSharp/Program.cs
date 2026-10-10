namespace Crap4CSharp;

// C# mirror of Java's crap4java.Main. The class is named Program (not Main)
// because C# forbids a member with the same name as its enclosing type;
// the entry point, usage text and max-score helper mirror the oracle.
internal static class Program
{
    public static int Main(string[] args)
    {
        string projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
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

    private const string UsageText =
        """
        Usage:
          crap4csharp            Analyze all C# files under src/
          crap4csharp --changed  Analyze changed C# files under src/
          crap4csharp <path...>  Analyze files, or for directory args analyze <dir>/src/**/*.cs
          crap4csharp --help     Print this help message
        """;

    internal static double MaxCrap(IEnumerable<MethodMetrics> metrics)
    {
        double max = 0.0;
        foreach (MethodMetrics metric in metrics)
        {
            if (metric.CrapScore is not null)
            {
                max = Math.Max(max, metric.CrapScore.Value);
            }
        }
        return max;
    }
}
