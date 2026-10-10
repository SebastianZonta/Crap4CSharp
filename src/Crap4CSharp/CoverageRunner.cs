namespace Crap4CSharp;

internal sealed class CoverageRunner
{
    private readonly ICommandExecutor _executor;

    internal CoverageRunner(ICommandExecutor executor)
    {
        _executor = executor;
    }

    internal void GenerateCoverage(string moduleRoot)
    {
        DeleteIfExists(Path.Combine(moduleRoot, "TestResults"));

        string resultsDirectory = Path.Combine(moduleRoot, "TestResults");
        int exit = _executor.Run(
            ["dotnet", "test", moduleRoot, "--collect:XPlat Code Coverage;Format=cobertura",
                "--results-directory", resultsDirectory],
            moduleRoot);
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
        if (Directory.Exists(path))
        {
            // Recursive reverse-order delete, mirroring Files.walk sorted reverseOrder:
            // children sort after their parents, so descending order deletes them first.
            IEnumerable<string> entries = Directory
                .EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories)
                .OrderByDescending(entry => entry, StringComparer.Ordinal);
            foreach (string entry in entries)
            {
                try
                {
                    if (File.Exists(entry))
                    {
                        File.Delete(entry);
                    }
                    else if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry);
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    throw new InvalidOperationException("Failed deleting stale coverage: " + entry, ex);
                }
            }
            Directory.Delete(path);
            return;
        }
        File.Delete(path);
    }
}
