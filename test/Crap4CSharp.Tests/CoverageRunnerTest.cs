namespace Crap4CSharp.Tests;

public sealed class CoverageRunnerTest : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory().FullName;

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void DeletesStaleCoverageAndRunsDotnetCoverageCommand()
    {
        string resultsDir = Path.Combine(_tempDir, "TestResults");
        Directory.CreateDirectory(resultsDir);
        File.WriteAllText(Path.Combine(resultsDir, "old.xml"), "stale");

        var executor = new RecordingExecutor(0);
        var runner = new CoverageRunner(executor);

        runner.GenerateCoverage(_tempDir);

        Assert.False(Directory.Exists(resultsDir));
        Assert.Single(executor.Commands);
        Assert.Equal(
            ["dotnet", "test", _tempDir, "--collect:XPlat Code Coverage;Format=cobertura",
                "--results-directory", resultsDir],
            executor.Commands[0]);
        Assert.Equal([_tempDir], executor.Directories);
    }

    [Fact]
    public void FailsWhenCoverageCommandFails()
    {
        var executor = new RecordingExecutor(2);
        var runner = new CoverageRunner(executor);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => runner.GenerateCoverage(_tempDir));

        Assert.Equal("Coverage command failed with exit 2", ex.Message);
    }

    private sealed class RecordingExecutor(int exitCode) : ICommandExecutor
    {
        internal readonly List<IReadOnlyList<string>> Commands = [];
        internal readonly List<string> Directories = [];

        public int Run(IReadOnlyList<string> args, string workingDirectory)
        {
            Commands.Add(args);
            Directories.Add(workingDirectory);
            return exitCode;
        }
    }
}
