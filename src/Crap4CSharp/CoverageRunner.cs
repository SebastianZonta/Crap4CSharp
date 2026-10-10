namespace Crap4CSharp;

internal sealed class CoverageRunner
{
    private readonly ICommandExecutor _executor;

    internal CoverageRunner(ICommandExecutor executor)
    {
        _executor = executor;
    }

    internal void GenerateCoverage(string moduleRoot) =>
        GenerateCoverageAsync(moduleRoot).GetAwaiter().GetResult();

    internal async Task GenerateCoverageAsync(
        string moduleRoot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleRoot);
        cancellationToken.ThrowIfCancellationRequested();
        DeleteIfExists(Path.Combine(moduleRoot, "TestResults"));

        string resultsDirectory = Path.Combine(moduleRoot, "TestResults");
        int exit = await _executor.RunAsync(
            ["dotnet", "test", moduleRoot, "--collect:XPlat Code Coverage;Format=cobertura",
                "--results-directory", resultsDirectory],
            moduleRoot,
            cancellationToken).ConfigureAwait(false);
        if (exit != 0)
        {
            throw new InvalidOperationException("Coverage command failed with exit " + exit);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            File.Delete(path);
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException || ex is FileNotFoundException)
        {
            // Lost a delete/create race with a concurrent run; nothing left to do.
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Failed deleting stale coverage: " + path, ex);
        }
    }
}
