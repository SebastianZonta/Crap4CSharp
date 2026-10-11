namespace Crap4CSharp.Tests;

public sealed class CoverageRunnerTest : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory().FullName;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void DeletesStaleCoverageAndRunsDotnetCoverageCommand()
    {
        var resultsDir = Path.Combine(_tempDir, "TestResults");
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

        var ex = Assert.Throws<InvalidOperationException>(
            () => runner.GenerateCoverage(_tempDir));

        Assert.Equal("Coverage command failed with exit 2", ex.Message);
    }

    [Fact]
    public async Task AsyncPreCancelledTokenAbortsBeforeRunningCommand()
    {
        var executor = new RecordingExecutor(0);
        var runner = new CoverageRunner(executor);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => runner.GenerateCoverageAsync(_tempDir, cancelled.Token));

        Assert.Empty(executor.Commands);
    }

    [Fact]
    public async Task AsyncDeletesNestedTestResultsBeforeRunning()
    {
        var resultsDir = Path.Combine(_tempDir, "TestResults", "nested", "deep");
        Directory.CreateDirectory(resultsDir);
        await File.WriteAllTextAsync(Path.Combine(resultsDir, "old.xml"), "stale");

        var executor = new RecordingExecutor(0);
        var runner = new CoverageRunner(executor);

        await runner.GenerateCoverageAsync(_tempDir);

        Assert.False(Directory.Exists(Path.Combine(_tempDir, "TestResults")));
        Assert.Single(executor.Commands);
        Assert.Equal(
            ["dotnet", "test", _tempDir, "--collect:XPlat Code Coverage;Format=cobertura",
                "--results-directory", Path.Combine(_tempDir, "TestResults")],
            executor.Commands[0]);
        Assert.Equal([_tempDir], executor.Directories);
    }

    [Fact]
    public async Task AsyncMissingModuleRootThrowsDirectoryNotFound()
    {
        var missing = Path.Combine(_tempDir, "does-not-exist");
        var runner = new CoverageRunner(new ProcessCommandExecutor());

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => runner.GenerateCoverageAsync(missing));
    }

    [Fact]
    public async Task DeletesStaleCoverageFileAtTestResultsPath()
    {
        var resultsPath = Path.Combine(_tempDir, "TestResults");
        await File.WriteAllTextAsync(resultsPath, "stale");

        var executor = new RecordingExecutor(0);
        var runner = new CoverageRunner(executor);

        await runner.GenerateCoverageAsync(_tempDir);

        Assert.False(File.Exists(resultsPath));
        Assert.Single(executor.Commands);
    }

    [Fact]
    public async Task ThreeArgOverloadTargetsSolutionWithModuleResultsDir()
    {
        var moduleRoot = Path.Combine(_tempDir, "src", "Demo");
        Directory.CreateDirectory(moduleRoot);
        var target = Path.Combine(_tempDir, "App.sln");
        await File.WriteAllTextAsync(target, "");
        var stale = Path.Combine(moduleRoot, "TestResults", "old.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(stale)!);
        await File.WriteAllTextAsync(stale, "stale");

        var executor = new RecordingExecutor(0);
        var runner = new CoverageRunner(executor);

        await runner.GenerateCoverageAsync(target, moduleRoot);

        Assert.False(Directory.Exists(Path.Combine(moduleRoot, "TestResults")));
        Assert.Single(executor.Commands);
        Assert.Equal(
            ["dotnet", "test", target, "--collect:XPlat Code Coverage;Format=cobertura",
                "--results-directory", Path.Combine(moduleRoot, "TestResults")],
            executor.Commands[0]);
        Assert.Equal([moduleRoot], executor.Directories);
    }

    [Fact]
    public async Task ThreeArgOverloadFailsWhenCommandFails()
    {
        var runner = new CoverageRunner(new RecordingExecutor(2));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.GenerateCoverageAsync(Path.Combine(_tempDir, "App.sln"), _tempDir));

        Assert.Equal("Coverage command failed with exit 2", ex.Message);
    }

    [Fact]
    public async Task ThreeArgOverloadNullTargetThrows()
    {
        var runner = new CoverageRunner(new RecordingExecutor(0));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => runner.GenerateCoverageAsync(null!, _tempDir));
    }

    [Fact]
    public void WrapsDeleteFailureAsInvalidOperation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        var resultsDir = Path.Combine(_tempDir, "TestResults");
        Directory.CreateDirectory(resultsDir);
        File.WriteAllText(Path.Combine(resultsDir, "locked.xml"), "stale");
        const UnixFileMode readExecute =
            UnixFileMode.UserRead | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        const UnixFileMode readWriteExecute = readExecute | UnixFileMode.UserWrite;
        File.SetUnixFileMode(resultsDir, readExecute);
        try
        {
            var runner = new CoverageRunner(new RecordingExecutor(0));

            var ex = Assert.Throws<InvalidOperationException>(
                () => runner.GenerateCoverage(_tempDir));

            Assert.StartsWith("Failed deleting stale coverage:", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.SetUnixFileMode(resultsDir, readWriteExecute);
        }
    }

    private sealed class RecordingExecutor(int exitCode) : ICommandExecutor
    {
        internal readonly List<IReadOnlyList<string>> Commands = [];
        internal readonly List<string> Directories = [];

        public int Run(IReadOnlyList<string> args, string workingDirectory)
        {
            Commands.Add([.. args]);
            Directories.Add(workingDirectory);
            return exitCode;
        }
    }
}