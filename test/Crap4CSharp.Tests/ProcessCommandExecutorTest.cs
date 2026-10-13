using System.Diagnostics;

namespace Crap4CSharp.Tests;

public sealed class ProcessCommandExecutorTest : IDisposable
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
    public void ReturnsExitCodeFromLaunchedProcess()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        var exit = new ProcessCommandExecutor().Run(["/bin/sh", "-c", "exit 7"], _tempDir);

        Assert.Equal(7, exit);
    }

    [Fact]
    public void PassesArgumentsWithSpacesAsSingleArgvEntry()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        var exit = new ProcessCommandExecutor().Run(["/bin/sh", "-c", "exit \"$0\"", "7"], _tempDir);

        Assert.Equal(7, exit);
    }

    [Fact]
    public void ChildStdoutAndStderrAreDrainedWithoutThrowing()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        var exit = new ProcessCommandExecutor().Run(
            ["/bin/sh", "-c", "echo out; echo err >&2; exit 5"], _tempDir);

        Assert.Equal(5, exit);
    }

    [Fact]
    public async Task StreamsChildStdoutAndStderrVerbatimToWriter()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        using var writer = new StringWriter();

        var exit = await new ProcessCommandExecutor().RunAsync(
            ["/bin/sh", "-c", "echo out; echo err >&2; exit 5"], _tempDir, writer);

        Assert.Equal(5, exit);
        var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("out", lines);
        Assert.Contains("err", lines);
    }

    [Fact]
    public async Task NullWriterDrainsWithoutWriting()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var exit = await new ProcessCommandExecutor().RunAsync(
            ["/bin/sh", "-c", "echo out; echo err >&2; exit 5"], _tempDir, null);

        Assert.Equal(5, exit);
    }

    [Fact]
    public async Task MissingWorkingDirectoryThrowsDirectoryNotFound()
    {
        var missing = Path.Combine(_tempDir, "does-not-exist");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => new ProcessCommandExecutor().RunAsync(["/bin/sh", "-c", "exit 0"], missing));
    }

    [Fact]
    public async Task EmptyCommandThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => new ProcessCommandExecutor().RunAsync([], _tempDir));

        Assert.Equal("args", ex.ParamName);
    }

    [Fact]
    public async Task NullCommandThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => new ProcessCommandExecutor().RunAsync(null!, _tempDir));
    }

    [Fact]
    public async Task PreCancelledTokenKillsLongRunningProcessFast()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new ProcessCommandExecutor().RunAsync(
                ["/bin/sh", "-c", "sleep 28734"], _tempDir, cancelled.Token));

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            "Cancelled sleep took " + stopwatch.Elapsed);
        AssertNoProcessMatching("sleep 28734");
    }

    private static void AssertNoProcessMatching(string pattern)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (true)
        {
            using var pgrep = Process.Start(
                new ProcessStartInfo("pgrep", ["-f", pattern])
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                })!;
            pgrep.WaitForExit();
            if (pgrep.ExitCode != 0)
            {
                return;
            }
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Process still alive after cancel: " + pattern);
            }
            Thread.Sleep(100);
        }
    }
}