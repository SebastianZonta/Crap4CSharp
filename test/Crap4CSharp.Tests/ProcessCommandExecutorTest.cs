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
            // best-effort cleanup
        }
        catch (UnauthorizedAccessException)
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public void ReturnsExitCodeFromLaunchedProcess()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        int exit = new ProcessCommandExecutor().Run(["/bin/sh", "-c", "exit 7"], _tempDir);

        Assert.Equal(7, exit);
    }

    [Fact]
    public void PassesArgumentsWithSpacesAsSingleArgvEntry()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        // The -c script contains a space and quotes; via ArgumentList it must
        // arrive as one argv entry, so $0 binds to "7" and the exit code is 7.
        int exit = new ProcessCommandExecutor().Run(["/bin/sh", "-c", "exit \"$0\"", "7"], _tempDir);

        Assert.Equal(7, exit);
    }

    [Fact]
    public async Task MissingWorkingDirectoryThrowsDirectoryNotFound()
    {
        string missing = Path.Combine(_tempDir, "does-not-exist");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => new ProcessCommandExecutor().RunAsync(["/bin/sh", "-c", "exit 0"], missing));
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
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
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
