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

    private static readonly string[] ResultRuns = ["run-b", "run-a"];

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

    [Fact]
    public void SortsMixedKnownAndUnknownScoresWithUnknownsLast()
    {
        string sourceRoot = Path.Combine(_tempDir, "src", "Demo");
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(sourceRoot, "Sample.cs"), """
            namespace Demo;
            class Sample {
                int Known(bool a) {
                    if (a) {
                        return 1;
                    }
                    return 0;
                }
                int Mystery() {
                    return 2;
                }
                int Plain(bool a) {
                    if (a) {
                        return 1;
                    }
                    return 0;
                }
                int Other() {
                    return 3;
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
                            <method name="Known" signature="()V">
                              <lines>
                                <line number="4" hits="2"/>
                                <line number="8" hits="2"/>
                              </lines>
                            </method>
                            <method name="Plain" signature="()V">
                              <lines>
                                <line number="13" hits="0"/>
                                <line number="17" hits="0"/>
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
        string report = output.ToString();
        Assert.True(report.IndexOf("Plain", StringComparison.Ordinal) < report.IndexOf("Known", StringComparison.Ordinal));
        Assert.True(report.IndexOf("Known", StringComparison.Ordinal) < report.IndexOf("Mystery", StringComparison.Ordinal));
        Assert.True(report.IndexOf("Mystery", StringComparison.Ordinal) < report.IndexOf("Other", StringComparison.Ordinal));
    }

    [Fact]
    public void PicksSmallestCoberturaXmlWhenSeveralExist()
    {
        string sourceRoot = Path.Combine(_tempDir, "src", "Demo");
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(sourceRoot, "Sample.cs"), """
            namespace Demo;

            class Sample {
                int Alpha() {
                    return 1;
                }
            }
            """);
        using var output = new StringWriter();
        using var err = new StringWriter();
        var coverageRunner = new CoverageRunner(new FakeExecutor((_, _) =>
        {
            foreach (string run in ResultRuns)
            {
                string dir = Path.Combine(_tempDir, "TestResults", run);
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "coverage.cobertura.xml"), """
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
            }
            return 0;
        }));

        int exit = new CliApplication(_tempDir, output, err, coverageRunner)
            .Execute([Path.Combine("src", "Demo", "Sample.cs")]);

        Assert.Equal(0, exit);
        Assert.Contains("Sample", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Warning: Cobertura XML not found", err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleRootForAcceptsDirectoryAndOutsidePaths()
    {
        string moduleRoot = Path.Combine(_tempDir, "tools", "m");
        Directory.CreateDirectory(Path.Combine(moduleRoot, "src"));
        File.WriteAllText(Path.Combine(moduleRoot, "Module.csproj"), "<Project/>");

        Assert.Equal(moduleRoot, CliApplication.ModuleRootFor(_tempDir, moduleRoot));
        Assert.Equal(
            _tempDir,
            CliApplication.ModuleRootFor(
                _tempDir, Path.Combine(Path.GetTempPath(), "crap4csharp-elsewhere", "x.cs")));
    }

    [Fact]
    public void ModuleRootFallsBackWhenCsprojScanDenied()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        string moduleRoot = Path.Combine(_tempDir, "tools", "locked");
        string source = Path.Combine(moduleRoot, "src", "Sample.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, "class Sample {}");
        // Write-only: listing the directory (as HasCsproj does) fails.
        const UnixFileMode writeExecuteOnly =
            UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        const UnixFileMode restored =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        File.SetUnixFileMode(moduleRoot, writeExecuteOnly);
        try
        {
            Assert.Equal(_tempDir, CliApplication.ModuleRootFor(_tempDir, source));
        }
        finally
        {
            File.SetUnixFileMode(moduleRoot, restored);
        }
    }

    [Fact]
    public async Task ChangedModeAnalyzesOnlyChangedFilesUnderSrc()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        Run("git init", _tempDir);
        Run("git config user.email test@example.com", _tempDir);
        Run("git config user.name test", _tempDir);

        string sourceRoot = Path.Combine(_tempDir, "src", "Demo");
        Directory.CreateDirectory(sourceRoot);
        string changed = Path.Combine(sourceRoot, "Sample.cs");
        await File.WriteAllTextAsync(changed, "class Sample {}\n");
        string otherDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(otherDir);
        await File.WriteAllTextAsync(Path.Combine(otherDir, "Other.cs"), "class Other {}\n");
        Run("git add .", _tempDir);
        Run("git commit -m init", _tempDir);
        await File.WriteAllTextAsync(changed, "class Sample { int M() { return 1; } }\n");

        using var output = new StringWriter();
        using var err = new StringWriter();

        int exit = await new CliApplication(_tempDir, output, err, NoopCoverage).ExecuteAsync(["--changed"]);

        Assert.Equal(0, exit);
        Assert.Contains("Sample", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Warning: Cobertura XML not found", err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SortsNullScoresFromModuleWithoutCoverageAfterScoredModule()
    {
        // The scored module sorts first ("aaa" < "bbb"), so the top-level
        // sort compares (scored, null): the only order reaching every arm.
        string moduleA = Path.Combine(_tempDir, "aaa");
        string srcA = Path.Combine(moduleA, "src");
        Directory.CreateDirectory(srcA);
        File.WriteAllText(Path.Combine(moduleA, "A.csproj"), "<Project/>");
        File.WriteAllText(Path.Combine(srcA, "Known.cs"), """
            namespace Demo;
            class Known {
                int Alpha() {
                    return 1;
                }
            }
            """);
        string moduleB = Path.Combine(_tempDir, "bbb");
        string srcB = Path.Combine(moduleB, "src");
        Directory.CreateDirectory(srcB);
        File.WriteAllText(Path.Combine(moduleB, "B.csproj"), "<Project/>");
        File.WriteAllText(Path.Combine(srcB, "Mystery.cs"), """
            namespace Demo;
            class Mystery {
                int Other() {
                    return 3;
                }
            }
            """);
        using var output = new StringWriter();
        using var err = new StringWriter();
        var coverageRunner = new CoverageRunner(new FakeExecutor((_, directory) =>
        {
            if (directory == moduleA)
            {
                string dir = Path.Combine(moduleA, "TestResults", "run1");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "coverage.cobertura.xml"), """
                    <coverage>
                      <packages>
                        <package name="Demo">
                          <classes>
                            <class name="Demo/Known" filename="Known.cs">
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
            }
            return 0;
        }));

        int exit = new CliApplication(_tempDir, output, err, coverageRunner)
            .Execute([Path.Combine("aaa", "src", "Known.cs"), Path.Combine("bbb", "src", "Mystery.cs")]);

        Assert.Equal(0, exit);
        string report = output.ToString();
        Assert.True(report.IndexOf("Alpha", StringComparison.Ordinal) < report.IndexOf("Other", StringComparison.Ordinal));
        Assert.Contains("Warning: Cobertura XML not found", err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ThresholdExceededOverloadUsesGivenThreshold()
    {
        Assert.False(CliApplication.ThresholdExceeded(8.0, 8.0));
        Assert.True(CliApplication.ThresholdExceeded(8.1, 8.0));
        Assert.False(CliApplication.ThresholdExceeded(5.0, 8.0));
        Assert.True(CliApplication.ThresholdExceeded(5.0, 4.0));
        Assert.False(CliApplication.ThresholdExceeded(0.0, 8.0));
    }

    [Fact]
    public void CustomThresholdGatesExitCodeAndMessage()
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
        string rel = Path.Combine("src", "Demo", "Sample.cs");

        using (var output = new StringWriter())
        using (var err = new StringWriter())
        {
            int exit = new CliApplication(_tempDir, output, err, coverageRunner)
                .Execute([rel, "--threshold", "1000"]);
            Assert.Equal(0, exit);
            Assert.DoesNotContain("CRAP threshold exceeded", err.ToString(), StringComparison.Ordinal);
        }

        using (var output = new StringWriter())
        using (var err = new StringWriter())
        {
            int exit = new CliApplication(_tempDir, output, err, coverageRunner)
                .Execute([rel, "--threshold=1.5"]);
            Assert.Equal(2, exit);
            Assert.Contains("> 1.5", err.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ResolveRootHandlesNullDirAndFile()
    {
        using var output = new StringWriter();
        using var err = new StringWriter();
        var app = new CliApplication(_tempDir, output, err, NoopCoverage);
        string root = Path.GetFullPath(_tempDir);

        Assert.Equal(root, app.ResolveRoot(null));
        Assert.Equal(root, app.ResolveRoot("  "));

        string sub = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(sub);
        Assert.Equal(Path.GetFullPath(sub), app.ResolveRoot("sub"));

        string file = Path.Combine(sub, "App.sln");
        File.WriteAllText(file, "");
        Assert.Equal(Path.GetFullPath(sub), app.ResolveRoot(Path.Combine("sub", "App.sln")));
        Assert.Equal(Path.GetFullPath(sub), app.ResolveRoot(file));
    }

    [Fact]
    public void OutputFlagWritesReportFileMatchingStdout()
    {
        string sourceRoot = Path.Combine(_tempDir, "src", "Demo");
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(sourceRoot, "Sample.cs"), """
            namespace Demo;

            class Sample {
                int Alpha() {
                    return 1;
                }
            }
            """);
        using var output = new StringWriter();
        using var err = new StringWriter();

        int exit = new CliApplication(_tempDir, output, err, NoopCoverage)
            .Execute([Path.Combine("src", "Demo", "Sample.cs"), "--output", "report.txt"]);

        Assert.Equal(0, exit);
        string file = Path.Combine(_tempDir, "report.txt");
        Assert.True(File.Exists(file));
        Assert.Equal(output.ToString(), File.ReadAllText(file));
    }

    [Fact]
    public void JsonFormatProducesJsonArray()
    {
        string sourceRoot = Path.Combine(_tempDir, "src", "Demo");
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(sourceRoot, "Sample.cs"), """
            namespace Demo;

            class Sample {
                int Alpha() {
                    return 1;
                }
            }
            """);
        using var output = new StringWriter();
        using var err = new StringWriter();

        int exit = new CliApplication(_tempDir, output, err, NoopCoverage)
            .Execute([Path.Combine("src", "Demo", "Sample.cs"), "--format", "json"]);

        Assert.Equal(0, exit);
        string report = output.ToString();
        Assert.StartsWith("[\n", report, StringComparison.Ordinal);
        Assert.Contains("\"method\": \"Alpha\"", report, StringComparison.Ordinal);
    }

    [Fact]
    public void CsvFormatProducesHeaderRow()
    {
        string sourceRoot = Path.Combine(_tempDir, "src", "Demo");
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(sourceRoot, "Sample.cs"), """
            namespace Demo;

            class Sample {
                int Alpha() {
                    return 1;
                }
            }
            """);
        using var output = new StringWriter();
        using var err = new StringWriter();

        int exit = new CliApplication(_tempDir, output, err, NoopCoverage)
            .Execute([Path.Combine("src", "Demo", "Sample.cs"), "--format=csv"]);

        Assert.Equal(0, exit);
        string report = output.ToString();
        Assert.StartsWith("method,class,cc,coverage,crap\n", report, StringComparison.Ordinal);
        Assert.Contains("Alpha", report, StringComparison.Ordinal);
    }

    private const string TwoMethodSource = """
        namespace Demo;

        class Sample {
            int Simple() {
                return 1;
            }
            int Branchy(bool a) {
                if (a) {
                    return 1;
                }
                return 0;
            }
        }
        """;

    private string WriteTwoMethodSample()
    {
        string sourceRoot = Path.Combine(_tempDir, "src", "Demo");
        Directory.CreateDirectory(sourceRoot);
        string source = Path.Combine(sourceRoot, "Sample.cs");
        File.WriteAllText(source, TwoMethodSource);
        return Path.Combine("src", "Demo", "Sample.cs");
    }

    [Fact]
    public void MinCcFiltersLowComplexityMethods()
    {
        string rel = WriteTwoMethodSample();
        using var output = new StringWriter();
        using var err = new StringWriter();

        int exit = new CliApplication(_tempDir, output, err, NoopCoverage)
            .Execute([rel, "--min-cc", "2"]);

        Assert.Equal(0, exit);
        string report = output.ToString();
        Assert.Contains("Branchy", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Simple", report, StringComparison.Ordinal);
    }

    [Fact]
    public void TopKeepsOnlyNWorstMethods()
    {
        string sourceRoot = Path.Combine(_tempDir, "src", "Demo");
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(sourceRoot, "Sample.cs"), TwoMethodSource);
        string resultsDir = Path.Combine(_tempDir, "TestResults", "run1");
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
                            <method name="Simple" signature="()V">
                              <lines>
                                <line number="4" hits="1"/>
                                <line number="5" hits="1"/>
                              </lines>
                            </method>
                            <method name="Branchy" signature="()V">
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
            return 0;
        }));
        string rel = Path.Combine("src", "Demo", "Sample.cs");
        using var output = new StringWriter();
        using var err = new StringWriter();

        int exit = new CliApplication(_tempDir, output, err, coverageRunner)
            .Execute([rel, "--top", "1"]);

        Assert.Equal(0, exit);
        string report = output.ToString();
        Assert.Contains("Branchy", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Simple", report, StringComparison.Ordinal);
    }

    [Fact]
    public void NoCoverageSkipsCoverageRunner()
    {
        string rel = WriteTwoMethodSample();
        int calls = 0;
        var countingRunner = new CoverageRunner(new FakeExecutor((_, _) =>
        {
            calls++;
            return 0;
        }));
        using var output = new StringWriter();
        using var err = new StringWriter();

        int exit = new CliApplication(_tempDir, output, err, countingRunner)
            .Execute([rel, "--no-coverage"]);

        Assert.Equal(0, exit);
        Assert.Equal(0, calls);
        Assert.Contains("Sample", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void VersionFlagPrintsVersion()
    {
        using var output = new StringWriter();
        using var err = new StringWriter();

        int exit = new CliApplication(_tempDir, output, err, NoopCoverage)
            .Execute(["--version"]);

        Assert.Equal(0, exit);
        Assert.Contains("crap4csharp", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownOptionReturnsOne()
    {
        using var output = new StringWriter();
        using var err = new StringWriter();

        int exit = new CliApplication(_tempDir, output, err, NoopCoverage)
            .Execute(["--bogus"]);

        Assert.Equal(1, exit);
        Assert.Contains("Unknown option", err.ToString(), StringComparison.Ordinal);
    }

    private static void Run(string command, string dir)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("sh", $"-c \"{command}\"")
        {
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = System.Diagnostics.Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(output);
        }
    }
}
