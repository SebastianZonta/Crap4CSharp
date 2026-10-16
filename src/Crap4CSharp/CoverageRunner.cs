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
        // Shelter prior results aside instead of deleting them: a failed run
        // restores them (and still throws), so red coverage never wipes the
        // last good report.
        var resultsDir = Path.Combine(moduleRoot, ToolConstants.TestResultsDirName);
        var backupDir = resultsDir + ".crap4csharp-bak-" + Guid.NewGuid().ToString("N");
        var shelteredDir = false;
        var shelteredFile = false;
        if (Directory.Exists(resultsDir))
        {
            Directory.Move(resultsDir, backupDir);
            shelteredDir = true;
        }
        else if (File.Exists(resultsDir))
        {
            File.Move(resultsDir, backupDir);
            shelteredFile = true;
        }
        var succeeded = false;
        try
        {
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
            succeeded = true;
        }
        finally
        {
            if (succeeded)
            {
                if (shelteredDir || shelteredFile)
                {
                    FileSystemHelpers.DeleteIfExists(backupDir);
                }
            }
            else
            {
                RestoreSheltered(resultsDir, backupDir, shelteredDir, shelteredFile);
            }
        }
    }

    private static void RestoreSheltered(string resultsDir, string backupDir, bool shelteredDir, bool shelteredFile)
    {
        if (!shelteredDir && !shelteredFile)
        {
            return;
        }
        // Best effort: the original failure is already propagating.
        FileSystemHelpers.DeleteIfExists(resultsDir);
        try
        {
            if (shelteredDir)
            {
                Directory.Move(backupDir, resultsDir);
            }
            else
            {
                File.Move(backupDir, resultsDir);
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is DirectoryNotFoundException)
        {
        }
    }

    internal static IReadOnlyList<string> CoverageCommand(string testTarget, string moduleRoot) =>
        ["dotnet", "test", testTarget, "--collect:XPlat Code Coverage;Format=cobertura",
            "--results-directory", Path.Combine(moduleRoot, ToolConstants.TestResultsDirName)];
}