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

    internal Task GenerateCoverageAsync(
        string moduleRoot, CancellationToken cancellationToken = default) =>
        GenerateCoverageAsync(moduleRoot, moduleRoot, cancellationToken);

    internal async Task GenerateCoverageAsync(
        string testTarget, string moduleRoot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(testTarget);
        ArgumentNullException.ThrowIfNull(moduleRoot);
        cancellationToken.ThrowIfCancellationRequested();
        FileSystemHelpers.DeleteIfExists(Path.Combine(moduleRoot, ToolConstants.TestResultsDirName));

        var resultsDirectory = Path.Combine(moduleRoot, ToolConstants.TestResultsDirName);
        var exit = await _executor.RunAsync(
            ["dotnet", "test", testTarget, "--collect:XPlat Code Coverage;Format=cobertura",
                "--results-directory", resultsDirectory],
            moduleRoot,
            cancellationToken).ConfigureAwait(false);
        if (exit != 0)
        {
            throw new InvalidOperationException("Coverage command failed with exit " + exit);
        }
    }
}