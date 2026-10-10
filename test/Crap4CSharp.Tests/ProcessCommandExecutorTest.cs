namespace Crap4CSharp.Tests;

public sealed class ProcessCommandExecutorTest : IDisposable
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
    public void ReturnsExitCodeFromLaunchedProcess()
    {
        int exit = new ProcessCommandExecutor().Run(["/bin/sh", "-c", "exit 7"], _tempDir);

        Assert.Equal(7, exit);
    }

    [Fact]
    public void PassesArgumentsWithSpacesAsSingleArgvEntry()
    {
        // The -c script contains a space and quotes; via ArgumentList it must
        // arrive as one argv entry, so $0 binds to "7" and the exit code is 7.
        int exit = new ProcessCommandExecutor().Run(["/bin/sh", "-c", "exit \"$0\"", "7"], _tempDir);

        Assert.Equal(7, exit);
    }
}
