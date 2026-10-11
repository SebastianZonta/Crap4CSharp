namespace Crap4CSharp.Tests;

public sealed class SolutionExpansionTest : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("crap4csharp-slnx-").FullName;

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

    private sealed class FakeExecutor : ICommandExecutor
    {
        private readonly Func<IReadOnlyList<string>, string, int> _run;
        public FakeExecutor(Func<IReadOnlyList<string>, string, int> run) => _run = run;
        public int Run(IReadOnlyList<string> command, string directory) => _run(command, directory);
    }

    private static readonly CoverageRunner _noopCoverage =
        new(new FakeExecutor((_, _) => 0));

    private void WriteFakeSolution()
    {
        File.WriteAllText(Path.Combine(_tempDir, "App.slnx"), "");
        WriteProject("proj-a", "SampleA", "AlphaA");
        WriteProject("proj-b", "SampleB", "AlphaB");
        var skipped = Path.Combine(_tempDir, "proj-a", "bin", "Skipped.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(skipped)!);
        File.WriteAllText(skipped, """
            namespace DemoA;
            class Skipped {
                int SkippedMethod() {
                    return 0;
                }
            }
            """);
    }

    private void WriteProject(string dir, string className, string method)
    {
        var projectDir = Path.Combine(_tempDir, dir);
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, dir + ".csproj"), "<Project/>");
        File.WriteAllText(Path.Combine(projectDir, className + ".cs"), $$"""
            namespace Demo;
            class {{className}} {
                int {{method}}() {
                    return 1;
                }
            }
            """);
    }

    [Fact]
    public void PositionalSlnxAnalyzesAllFiles()
    {
        WriteFakeSolution();
        using var output = new StringWriter();
        using var err = new StringWriter();

        var exit = Program.Run(["App.slnx", "--no-coverage"], _tempDir, output, err, _noopCoverage);

        Assert.Equal(0, exit);
        var report = output.ToString();
        Assert.Contains("SampleA", report, StringComparison.Ordinal);
        Assert.Contains("SampleB", report, StringComparison.Ordinal);
        Assert.DoesNotContain("SkippedMethod", report, StringComparison.Ordinal);
    }

    [Fact]
    public void PositionalSubdirAnalyzesItsFiles()
    {
        WriteFakeSolution();
        using var output = new StringWriter();
        using var err = new StringWriter();

        var exit = Program.Run(["proj-a", "--no-coverage"], _tempDir, output, err, _noopCoverage);

        Assert.Equal(0, exit);
        var report = output.ToString();
        Assert.Contains("SampleA", report, StringComparison.Ordinal);
        Assert.DoesNotContain("SampleB", report, StringComparison.Ordinal);
    }

    [Fact]
    public void PositionalCsprojAnalyzesOnlyItsProject()
    {
        WriteFakeSolution();
        using var output = new StringWriter();
        using var err = new StringWriter();

        var exit = Program.Run(["proj-a/proj-a.csproj", "--no-coverage"], _tempDir, output, err, _noopCoverage);

        Assert.Equal(0, exit);
        var report = output.ToString();
        Assert.Contains("SampleA", report, StringComparison.Ordinal);
        Assert.DoesNotContain("SampleB", report, StringComparison.Ordinal);
        Assert.DoesNotContain("SkippedMethod", report, StringComparison.Ordinal);
    }

    [Fact]
    public void NoArgsAllSrcAnalyzesAllFilesWithoutSrcDir()
    {
        WriteFakeSolution();
        using var output = new StringWriter();
        using var err = new StringWriter();

        var exit = Program.Run(["--no-coverage"], _tempDir, output, err, _noopCoverage);

        Assert.Equal(0, exit);
        var report = output.ToString();
        Assert.Contains("SampleA", report, StringComparison.Ordinal);
        Assert.Contains("SampleB", report, StringComparison.Ordinal);
    }
}