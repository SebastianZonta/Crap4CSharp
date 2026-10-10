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
        FileSystemHelpers.DeleteIfExists(Path.Combine(moduleRoot, ToolConstants.TestResultsDirName));

        string resultsDirectory = Path.Combine(moduleRoot, ToolConstants.TestResultsDirName);
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
}
