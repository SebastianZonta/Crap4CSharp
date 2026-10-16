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
        WriteProjectUnder(_tempDir, dir, className, $$"""
            namespace Demo;
            class {{className}} {
                int {{method}}() {
                    return 1;
                }
            }
            """);
    }

    private static void WriteProjectUnder(string root, string dir, string className, string source)
    {
        var projectDir = Path.Combine(root, dir);
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, dir + ".csproj"), "<Project/>");
        File.WriteAllText(Path.Combine(projectDir, className + ".cs"), source);
    }

    private static void WriteCoverage(string projectDir, string xml)
    {
        var resultsDir = Path.Combine(projectDir, "TestResults", "run1");
        Directory.CreateDirectory(resultsDir);
        File.WriteAllText(Path.Combine(resultsDir, "coverage.cobertura.xml"), xml);
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

    [Fact]
    public void AbsoluteSlnxOutsideWorkspaceAgreesWithPathRunOnCoverage()
    {
        var targetDir = Directory.CreateTempSubdirectory("crap4csharp-slnx-target-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(targetDir, "App.slnx"), "");
            WriteProjectUnder(targetDir, "proj-a", "SampleA", """
                namespace Demo;

                class SampleA
                {
                    int Risky(int x)
                    {
                        int r = 0;
                        if (x > 1) r += 1;
                        if (x > 2) r += 2;
                        if (x > 3) r += 4;
                        if (x > 4) r += 8;
                        if (x > 5) r += 16;
                        if (x > 6) r += 32;
                        if (x > 7) r += 64;
                        if (x > 8) r += 128;
                        if (x > 9) r += 256;
                        return r;
                    }
                }
                """);
            var lines = string.Concat(Enumerable.Range(7, 12)
                .Select(n => $"                              <line number=\"{n}\" hits=\"0\"/>\n"));
            WriteCoverage(Path.Combine(targetDir, "proj-a"), $$"""
                <coverage>
                  <packages>
                    <package name="Demo">
                      <classes>
                        <class name="Demo/SampleA" filename="SampleA.cs">
                          <methods>
                            <method name="Risky" signature="()V">
                              <lines>
                {{lines}}              </lines>
                            </method>
                          </methods>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """);
            WriteProjectUnder(targetDir, "proj-b", "SampleB", """
                namespace Demo;

                class SampleB
                {
                    int Alpha()
                    {
                        return 1;
                    }
                }
                """);
            WriteCoverage(Path.Combine(targetDir, "proj-b"), """
                <coverage>
                  <packages>
                    <package name="Demo">
                      <classes>
                        <class name="Demo/SampleB" filename="SampleB.cs">
                          <methods>
                            <method name="Alpha" signature="()V">
                              <lines>
                                <line number="7" hits="1"/>
                                <line number="8" hits="1"/>
                              </lines>
                            </method>
                          </methods>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """);

            using var pathOutput = new StringWriter();
            using var pathErr = new StringWriter();
            var pathExit = Program.Run(
                ["--path", targetDir, "--no-coverage", "--no-progress"],
                _tempDir, pathOutput, pathErr, _noopCoverage);

            using var slnxOutput = new StringWriter();
            using var slnxErr = new StringWriter();
            var slnxExit = Program.Run(
                [Path.Combine(targetDir, "App.slnx"), "--no-coverage", "--no-progress"],
                _tempDir, slnxOutput, slnxErr, _noopCoverage);

            Assert.Equal(2, pathExit);
            Assert.Equal(pathExit, slnxExit);
            Assert.Equal(pathOutput.ToString(), slnxOutput.ToString());
            Assert.Contains("0.0%", slnxOutput.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("N/A", slnxOutput.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("Cobertura XML not found", slnxErr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(targetDir, recursive: true);
        }
    }
}