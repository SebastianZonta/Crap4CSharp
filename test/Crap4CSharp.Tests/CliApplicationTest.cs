namespace Crap4CSharp.Tests;

public sealed class CliApplicationTest : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("crap4csharp-app-").FullName;

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
        GC.SuppressFinalize(this);
    }

    private sealed class FakeExecutor : ICommandExecutor
    {
        private readonly Func<IReadOnlyList<string>, string, int> _run;
        public FakeExecutor(Func<IReadOnlyList<string>, string, int> run) => _run = run;
        public int Run(IReadOnlyList<string> command, string directory) => _run(command, directory);
    }

    private static readonly CoverageRunner NoopCoverage =
        new(new FakeExecutor((_, _) => 0));

    [Fact]
    public void ParseErrorsReturnUsageAndExitOne()
    {
        using var output = new StringWriter();
        using var err = new StringWriter();

        int exit = new CliApplication(_tempDir, output, err, NoopCoverage)
            .Execute(["--changed", Path.Combine("src", "Sample.cs")]);

        Assert.Equal(1, exit);
        Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--changed cannot be combined with file arguments", err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReturnsZeroWhenNoFilesAreFound()
    {
        using var output = new StringWriter();
        using var err = new StringWriter();

        int exit = new CliApplication(_tempDir, output, err, NoopCoverage)
            .Execute([]);

        Assert.Equal(0, exit);
        Assert.Contains("No C# files to analyze.", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void DoesNotWarnWhenCoberturaXmlExists()
    {
        string sourceRoot = Path.Combine(_tempDir, "src", "Demo");
        Directory.CreateDirectory(sourceRoot);
        string source = Path.Combine(sourceRoot, "Sample.cs");
        File.WriteAllText(source, """
            namespace Demo;

            class Sample {
                int Alpha() {
                    return 1;
                }
            }
            """);
        string resultsDir = Path.Combine(_tempDir, "TestResults", "run1");
        using var output = new StringWriter();
        using var err = new StringWriter();
        var coverageRunner = new CoverageRunner(new FakeExecutor((_, _) =>
        {
            Directory.CreateDirectory(resultsDir);
            File.WriteAllText(Path.Combine(resultsDir, "coverage.cobertura.xml"), """
                <coverage>
                  <packages>
                    <package name="Demo">
                      <classes>
                        <class name="Demo/Sample" filename="Sample.cs">
                          <methods>
                            <method name="Alpha" signature="()V">
                              <lines>
                                <line number="4" hits="1"/>
                                <line number="5" hits="1"/>
                              </lines>
                            </method>
                          </methods>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """);
            return 0;
        }));

        int exit = new CliApplication(_tempDir, output, err, coverageRunner)
            .Execute([Path.Combine("src", "Demo", "Sample.cs")]);

        Assert.Equal(0, exit);
        Assert.Contains("Sample", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Warning: Cobertura XML not found", err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitFileUsesOwningModuleForCoverageAndXml()
    {
        string moduleRoot = Path.Combine(_tempDir, "tools", "mutate4csharp");
        string sourceRoot = Path.Combine(moduleRoot, "src");
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(moduleRoot, "Module.csproj"), "<Project/>");
        string source = Path.Combine(sourceRoot, "Sample.cs");
        File.WriteAllText(source, """
            namespace Mutate4CSharp;

            class Sample {
                int Alpha() {
                    return 1;
                }
            }
            """);
        using var output = new StringWriter();
        using var err = new StringWriter();
        var directories = new List<string>();
        string resultsDir = Path.Combine(moduleRoot, "TestResults", "run1");
        var coverageRunner = new CoverageRunner(new FakeExecutor((_, directory) =>
        {
            directories.Add(directory);
            Directory.CreateDirectory(resultsDir);
            File.WriteAllText(Path.Combine(resultsDir, "coverage.cobertura.xml"), """
                <coverage>
                  <packages>
                    <package name="Mutate4CSharp">
                      <classes>
                        <class name="Mutate4CSharp/Sample" filename="Sample.cs">
                          <methods>
                            <method name="Alpha" signature="()V">
                              <lines>
                                <line number="4" hits="1"/>
                                <line number="5" hits="1"/>
                              </lines>
                            </method>
                          </methods>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """);
            return 0;
        }));

        int exit = new CliApplication(_tempDir, output, err, coverageRunner)
            .Execute([Path.Combine("tools", "mutate4csharp", "src", "Sample.cs")]);

        Assert.Equal(0, exit);
        Assert.Equal(new[] { moduleRoot }, directories);
        Assert.Contains("Mutate4CSharp.Sample", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Warning: Cobertura XML not found", err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ThresholdBreachReturnsTwoAndWritesThresholdToStderr()
    {
        string sourceRoot = Path.Combine(_tempDir, "src", "Demo");
        Directory.CreateDirectory(sourceRoot);
        string source = Path.Combine(sourceRoot, "Sample.cs");
        File.WriteAllText(source, """
            namespace Demo;

            class Sample {
                int Risky(int x) {
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
        string resultsDir = Path.Combine(_tempDir, "TestResults", "run1");
        using var output = new StringWriter();
        using var err = new StringWriter();
        var coverageRunner = new CoverageRunner(new FakeExecutor((_, _) =>
        {
            Directory.CreateDirectory(resultsDir);
            var lines = string.Concat(Enumerable.Range(4, 13)
                .Select(n => $"                              <line number=\"{n}\" hits=\"0\"/>\n"));
            File.WriteAllText(Path.Combine(resultsDir, "coverage.cobertura.xml"), $$"""
                <coverage>
                  <packages>
                    <package name="Demo">
                      <classes>
                        <class name="Demo/Sample" filename="Sample.cs">
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
            return 0;
        }));

        int exit = new CliApplication(_tempDir, output, err, coverageRunner)
            .Execute([Path.Combine("src", "Demo", "Sample.cs")]);

        Assert.Equal(2, exit);
        Assert.Contains("Risky", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("CRAP threshold exceeded:", err.ToString(), StringComparison.Ordinal);
        Assert.Contains("> 8.0", err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ThresholdExceededUsesStrictlyGreaterThanEight()
    {
        Assert.False(CliApplication.ThresholdExceeded(8.0));
        Assert.True(CliApplication.ThresholdExceeded(8.1));
    }

    [Fact]
    public void ModuleRootForFindsNearestAncestorWithCsproj()
    {
        string moduleRoot = Path.Combine(_tempDir, "tools", "mutate4csharp");
        string source = Path.Combine(moduleRoot, "src", "Sample.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(Path.Combine(moduleRoot, "Module.csproj"), "<Project/>");
        File.WriteAllText(source, "class Sample {}");

        string module = CliApplication.ModuleRootFor(_tempDir, source);

        Assert.Equal(moduleRoot, module);
    }
}
