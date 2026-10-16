using System.Diagnostics;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Crap4CSharp.Tests;

public sealed class ChangedFileDetectorTest : IDisposable
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
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void FindsModifiedAndUntrackedCsFiles()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        Run("git init", _tempDir);
        Run("git config user.email test@example.com", _tempDir);
        Run("git config user.name test", _tempDir);

        var src = Path.Combine(_tempDir, "src", "main", "cs", "demo");
        Directory.CreateDirectory(src);
        var tracked = Path.Combine(src, "Tracked.cs");
        File.WriteAllText(tracked, "class Tracked {}\n");

        Run("git add .", _tempDir);
        Run("git commit -m init", _tempDir);

        File.WriteAllText(tracked, "class Tracked { int x = 1; }\n");
        var untracked = Path.Combine(src, "NewFile.cs");
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
    public void FindsQuotedPathsWithSpaces()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        Run("git init", _tempDir);
        Run("git config user.email test@example.com", _tempDir);
        Run("git config user.name test", _tempDir);

        var src = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(src);
        var spaced = Path.Combine(src, "My File.cs");
        File.WriteAllText(spaced, "class Spaced {}\n");
        Run("git add .", _tempDir);
        Run("git commit -m init", _tempDir);
        File.WriteAllText(spaced, "class Spaced { int M() { return 1; } }\n");

        var changed = ChangedFileDetector.ChangedCsFiles(_tempDir);

        Assert.Equal(new[] { spaced }, changed);
    }

    [Fact]
    public void UnquoteGitPathDecodesQuotedPaths()
    {
        Assert.Equal("src/My File.cs", ChangedFileDetector.UnquoteGitPath("\"src/My File.cs\""));
        Assert.Equal("src/café.cs", ChangedFileDetector.UnquoteGitPath("\"src/caf\\303\\251.cs\""));
        Assert.Equal("src/a\"b.cs", ChangedFileDetector.UnquoteGitPath("\"src/a\\\"b.cs\""));
        Assert.Equal("src/Plain.cs", ChangedFileDetector.UnquoteGitPath("src/Plain.cs"));
        Assert.Equal("\"x", ChangedFileDetector.UnquoteGitPath("\"x"));
        Assert.Equal(string.Empty, ChangedFileDetector.UnquoteGitPath("\"\""));
    }

    [Fact]
    public void IncludesGitErrorOutputWhenStatusFails()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => ChangedFileDetector.ChangedCsFiles(_tempDir));

        Assert.Contains("not a git repository", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingGitBinaryThrowsCleanInvalidOperation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        var emptyBin = Path.Combine(_tempDir, "empty-bin");
        Directory.CreateDirectory(emptyBin);
        var previous = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", emptyBin);
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => ChangedFileDetector.ChangedCsFilesAsync(_tempDir));
            Assert.Equal("git status failed: could not start git process", error.Message);
            Assert.IsType<InvalidOperationException>(error);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previous);
        }
    }

    [Fact]
    public void FiltersChangedFilesToSrcTreeOnly()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        Run("git init", _tempDir);
        Run("git config user.email test@example.com", _tempDir);
        Run("git config user.name test", _tempDir);

        var mainSrc = Path.Combine(_tempDir, "src", "main", "cs", "demo");
        var otherDir = Path.Combine(_tempDir, "tests", "Crap4CSharp.Tests");
        Directory.CreateDirectory(mainSrc);
        Directory.CreateDirectory(otherDir);

        var tracked = Path.Combine(mainSrc, "Tracked.cs");
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
    public void FallsBackToWholeTreeWhenSrcIsMissing()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        Run("git init", _tempDir);
        Run("git config user.email test@example.com", _tempDir);
        Run("git config user.name test", _tempDir);

        var moduleDir = Path.Combine(_tempDir, "Domain");
        Directory.CreateDirectory(moduleDir);
        var tracked = Path.Combine(moduleDir, "Tracked.cs");
        File.WriteAllText(tracked, "class Tracked {}\n");
        Run("git add .", _tempDir);
        Run("git commit -m init", _tempDir);

        File.WriteAllText(tracked, "class Tracked { int x = 1; }\n");
        var untracked = Path.Combine(moduleDir, "NewFile.cs");
        File.WriteAllText(untracked, "class NewFile {}\n");

        var changed = ChangedFileDetector.ChangedCsFilesUnderSrc(_tempDir);

        Assert.Equal(new[] { untracked, tracked }, changed);
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

    [Fact]
    public async Task AsyncReturnsSameResultsAsSync()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        Run("git init", _tempDir);
        Run("git config user.email test@example.com", _tempDir);
        Run("git config user.name test", _tempDir);

        var src = Path.Combine(_tempDir, "src", "main", "cs", "demo");
        Directory.CreateDirectory(src);
        var tracked = Path.Combine(src, "Tracked.cs");
        await File.WriteAllTextAsync(tracked, "class Tracked {}\n");

        Run("git add .", _tempDir);
        Run("git commit -m init", _tempDir);

        await File.WriteAllTextAsync(tracked, "class Tracked { int x = 1; }\n");
        await File.WriteAllTextAsync(Path.Combine(src, "NewFile.cs"), "class NewFile {}\n");

        Assert.Equal(
            SyncChangedFiles(_tempDir),
            await ChangedFileDetector.ChangedCsFilesAsync(_tempDir));
        Assert.Equal(
            SyncChangedFilesUnderSrc(_tempDir),
            await ChangedFileDetector.ChangedCsFilesUnderSrcAsync(_tempDir));
    }

    [Fact]
    public async Task ProjectRootWithSpacesWorks()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        var spaced = Path.Combine(_tempDir, "dir with spaces");
        var src = Path.Combine(spaced, "src");
        Directory.CreateDirectory(src);
        Run("git init", spaced);
        Run("git config user.email test@example.com", spaced);
        Run("git config user.name test", spaced);

        var tracked = Path.Combine(src, "Tracked.cs");
        await File.WriteAllTextAsync(tracked, "class Tracked {}\n");
        Run("git add .", spaced);
        Run("git commit -m init", spaced);
        await File.WriteAllTextAsync(tracked, "class Tracked { int x = 1; }\n");

        Assert.Equal(
            new[] { tracked },
            await ChangedFileDetector.ChangedCsFilesAsync(spaced));
    }

    [Fact]
    public async Task PreCancelledTokenThrowsOperationCanceled()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        Run("git init", _tempDir);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => ChangedFileDetector.ChangedCsFilesAsync(_tempDir, cancelled.Token));
    }

    [Fact]
    public void MissingProjectRootThrowsDirectoryNotFound()
    {
        var missing = Path.Combine(_tempDir, "does-not-exist");

        Assert.Throws<DirectoryNotFoundException>(
            () => ChangedFileDetector.ChangedCsFiles(missing));
    }

    [Fact]
    public async Task NullProjectRootThrowsArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ChangedFileDetector.ChangedCsFilesAsync(null!));
    }

    [Fact]
    public void DetectsStagedRenameUnderNewName()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        Run("git init", _tempDir);
        Run("git config user.email test@example.com", _tempDir);
        Run("git config user.name test", _tempDir);

        var src = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "Old.cs"), "class Old {}\n");
        Run("git add .", _tempDir);
        Run("git commit -m init", _tempDir);
        Run("git mv src/Old.cs src/New.cs", _tempDir);

        var changed = ChangedFileDetector.ChangedCsFiles(_tempDir);

        Assert.Equal(new[] { Path.Combine(src, "New.cs") }, changed);
    }

    [Fact]
    public void DiffSelectsAddedAndModifiedFilesExcludesDeletions()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        Run("git init", _tempDir);
        Run("git config user.email test@example.com", _tempDir);
        Run("git config user.name test", _tempDir);

        var src = Path.Combine(_tempDir, "src", "Demo");
        Directory.CreateDirectory(src);
        var modified = Path.Combine(src, "Modified.cs");
        File.WriteAllText(modified, "class Modified {}\n");
        File.WriteAllText(Path.Combine(src, "Doomed.cs"), "class Doomed {}\n");
        File.WriteAllText(Path.Combine(_tempDir, "notes.txt"), "v1\n");
        Run("git add .", _tempDir);
        Run("git commit -m init", _tempDir);
        Run("git branch base", _tempDir);

        File.WriteAllText(modified, "class Modified { int M() { return 1; } }\n");
        var added = Path.Combine(src, "Added.cs");
        File.WriteAllText(added, "class Added {}\n");
        Run("git rm src/Demo/Doomed.cs", _tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "notes.txt"), "v2\n");
        Run("git add .", _tempDir);
        Run("git commit -m second", _tempDir);
        File.WriteAllText(Path.Combine(src, "Untracked.cs"), "class Untracked {}\n");

        var diff = ChangedFileDetector.DiffCsFiles(_tempDir, "base");

        Assert.Equal(new[] { added, modified }, diff);
    }

    [Fact]
    public void DiffRespectsSrcScope()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        Run("git init", _tempDir);
        Run("git config user.email test@example.com", _tempDir);
        Run("git config user.name test", _tempDir);

        var src = Path.Combine(_tempDir, "src", "Demo");
        var tests = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(tests);
        var tracked = Path.Combine(src, "Tracked.cs");
        File.WriteAllText(tracked, "class Tracked {}\n");
        File.WriteAllText(Path.Combine(tests, "Other.cs"), "class Other {}\n");
        Run("git add .", _tempDir);
        Run("git commit -m init", _tempDir);
        Run("git branch base", _tempDir);

        File.WriteAllText(tracked, "class Tracked { int M() { return 1; } }\n");
        File.WriteAllText(Path.Combine(tests, "Other.cs"), "class Other { int M() { return 1; } }\n");
        Run("git commit -am second", _tempDir);

        var diff = ChangedFileDetector.DiffCsFilesUnderSrc(_tempDir, "base");

        Assert.Equal(new[] { tracked }, diff);
    }

    [Fact]
    public void DiffFallsBackToWholeTreeWhenSrcIsMissing()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        Run("git init", _tempDir);
        Run("git config user.email test@example.com", _tempDir);
        Run("git config user.name test", _tempDir);

        var moduleDir = Path.Combine(_tempDir, "Domain");
        Directory.CreateDirectory(moduleDir);
        var tracked = Path.Combine(moduleDir, "Tracked.cs");
        File.WriteAllText(tracked, "class Tracked {}\n");
        Run("git add .", _tempDir);
        Run("git commit -m init", _tempDir);
        Run("git branch base", _tempDir);

        File.WriteAllText(tracked, "class Tracked { int M() { return 1; } }\n");
        Run("git commit -am second", _tempDir);

        var diff = ChangedFileDetector.DiffCsFilesUnderSrc(_tempDir, "base");

        Assert.Equal(new[] { tracked }, diff);
    }

    [Fact]
    public void DiffUnknownRefThrowsWithFetchHint()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        Run("git init", _tempDir);
        Run("git config user.email test@example.com", _tempDir);
        Run("git config user.name test", _tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "Tracked.cs"), "class Tracked {}\n");
        Run("git add .", _tempDir);
        Run("git commit -m init", _tempDir);

        var error = Assert.Throws<InvalidOperationException>(
            () => ChangedFileDetector.DiffCsFiles(_tempDir, "no-such-ref"));

        Assert.Contains("no-such-ref", error.Message, StringComparison.Ordinal);
        Assert.Contains("fetch", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DiffOutsideGitRepoThrows()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => ChangedFileDetector.DiffCsFiles(_tempDir, "base"));

        Assert.Contains("not a git repository", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiffAsyncReturnsSameResultsAsSync()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        Run("git init", _tempDir);
        Run("git config user.email test@example.com", _tempDir);
        Run("git config user.name test", _tempDir);

        var src = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(src);
        var tracked = Path.Combine(src, "Tracked.cs");
        await File.WriteAllTextAsync(tracked, "class Tracked {}\n");
        Run("git add .", _tempDir);
        Run("git commit -m init", _tempDir);
        Run("git branch base", _tempDir);
        await File.WriteAllTextAsync(tracked, "class Tracked { int M() { return 1; } }\n");
        Run("git commit -am second", _tempDir);

        Assert.Equal(
            SyncDiffFiles(_tempDir, "base"),
            await ChangedFileDetector.DiffCsFilesAsync(_tempDir, "base"));
        Assert.Equal(
            SyncDiffFilesUnderSrc(_tempDir, "base"),
            await ChangedFileDetector.DiffCsFilesUnderSrcAsync(_tempDir, "base"));
    }

    private static IReadOnlyList<string> SyncChangedFiles(string dir) =>
        ChangedFileDetector.ChangedCsFiles(dir);

    private static IReadOnlyList<string> SyncChangedFilesUnderSrc(string dir) =>
        ChangedFileDetector.ChangedCsFilesUnderSrc(dir);

    private static IReadOnlyList<string> SyncDiffFiles(string dir, string diffRef) =>
        ChangedFileDetector.DiffCsFiles(dir, diffRef);

    private static IReadOnlyList<string> SyncDiffFilesUnderSrc(string dir, string diffRef) =>
        ChangedFileDetector.DiffCsFilesUnderSrc(dir, diffRef);

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
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(output);
        }
    }
}