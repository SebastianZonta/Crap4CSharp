using System.Diagnostics;

namespace Crap4CSharp.Tests;

public class ChangedFileDetectorTest : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory().FullName;

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void FindsModifiedAndUntrackedCsFiles()
    {
        Run("git init", _tempDir);
        Run("git config user.email test@example.com", _tempDir);
        Run("git config user.name test", _tempDir);

        string src = Path.Combine(_tempDir, "src", "main", "cs", "demo");
        Directory.CreateDirectory(src);
        string tracked = Path.Combine(src, "Tracked.cs");
        File.WriteAllText(tracked, "class Tracked {}\n");

        Run("git add .", _tempDir);
        Run("git commit -m init", _tempDir);

        File.WriteAllText(tracked, "class Tracked { int x = 1; }\n");
        string untracked = Path.Combine(src, "NewFile.cs");
        File.WriteAllText(untracked, "class NewFile {}\n");
        File.WriteAllText(Path.Combine(_tempDir, "README.md"), "ignore me\n");

        var changed = ChangedFileDetector.ChangedCsFiles(_tempDir);

        Assert.Equal(
            new[]
            {
                Path.Combine(_tempDir, "src", "main", "cs", "demo", "NewFile.cs"),
                Path.Combine(_tempDir, "src", "main", "cs", "demo", "Tracked.cs"),
            },
            changed);
    }

    [Fact]
    public void IncludesGitErrorOutputWhenStatusFails()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => ChangedFileDetector.ChangedCsFiles(_tempDir));

        Assert.Contains("not a git repository", error.Message);
    }

    [Fact]
    public void FiltersChangedFilesToSrcTreeOnly()
    {
        Run("git init", _tempDir);
        Run("git config user.email test@example.com", _tempDir);
        Run("git config user.name test", _tempDir);

        string mainSrc = Path.Combine(_tempDir, "src", "main", "cs", "demo");
        string otherDir = Path.Combine(_tempDir, "tests", "Crap4CSharp.Tests");
        Directory.CreateDirectory(mainSrc);
        Directory.CreateDirectory(otherDir);

        string tracked = Path.Combine(mainSrc, "Tracked.cs");
        File.WriteAllText(tracked, "class Tracked {}\n");
        Run("git add .", _tempDir);
        Run("git commit -m init", _tempDir);

        File.WriteAllText(tracked, "class Tracked { int x = 1; }\n");
        File.WriteAllText(Path.Combine(otherDir, "ChangedFileDetectorTest.cs"), "class ChangedFileDetectorTest {}\n");

        var changed = ChangedFileDetector.ChangedCsFilesUnderSrc(_tempDir);

        Assert.Equal(
            new[] { Path.Combine(_tempDir, "src", "main", "cs", "demo", "Tracked.cs") },
            changed);
    }

    [Fact]
    public void CandidateLineRequiresAtLeastFourCharacters()
    {
        Assert.False(ChangedFileDetector.IsCandidateLine(null));
        Assert.False(ChangedFileDetector.IsCandidateLine(""));
        Assert.False(ChangedFileDetector.IsCandidateLine("abc"));
        Assert.True(ChangedFileDetector.IsCandidateLine("abcd"));
    }

    [Fact]
    public void RenameTargetUsesReplacementSideEvenAtStartOfString()
    {
        Assert.Equal("src/New.cs", ChangedFileDetector.RenameTarget("src/Old.cs -> src/New.cs"));
        Assert.Equal("New.cs", ChangedFileDetector.RenameTarget(" -> New.cs"));
        Assert.Equal("Plain.cs", ChangedFileDetector.RenameTarget("Plain.cs"));
    }

    private static void Run(string command, string dir)
    {
        var startInfo = new ProcessStartInfo("sh", $"-c \"{command}\"")
        {
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(output);
        }
    }
}
