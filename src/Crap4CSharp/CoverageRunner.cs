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
        GenerateCoverageAsync(moduleRoot, moduleRoot, null, cancellationToken);

    internal Task GenerateCoverageAsync(
        string testTarget, string moduleRoot, CancellationToken cancellationToken = default) =>
        GenerateCoverageAsync(testTarget, moduleRoot, null, cancellationToken);

    internal async Task GenerateCoverageAsync(
        string testTarget, string moduleRoot, TextWriter? output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(testTarget);
        ArgumentNullException.ThrowIfNull(moduleRoot);
        cancellationToken.ThrowIfCancellationRequested();
        FileSystemHelpers.DeleteIfExists(Path.Combine(moduleRoot, ToolConstants.TestResultsDirName));

        var command = CoverageCommand(testTarget, moduleRoot);
        var exit = await _executor.RunAsync(
            command,
            moduleRoot,
            output,
            cancellationToken).ConfigureAwait(false);
        if (exit != 0)
        {
            throw new InvalidOperationException("Coverage command failed with exit " + exit);
        }
    }

    internal static IReadOnlyList<string> CoverageCommand(string testTarget, string moduleRoot) =>
        ["dotnet", "test", testTarget, "--collect:XPlat Code Coverage;Format=cobertura",
            "--results-directory", Path.Combine(moduleRoot, ToolConstants.TestResultsDirName)];
}