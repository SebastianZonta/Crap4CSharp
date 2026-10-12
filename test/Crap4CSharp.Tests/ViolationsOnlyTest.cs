namespace Crap4CSharp.Tests;

public sealed class ViolationsOnlyTest : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("crap4csharp-viol-").FullName;

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

    [Fact]
    public void ViolationsOnlyFlagDefaultsFalseAndParses()
    {
        Assert.False(CliArgumentsParser.Parse(Array.Empty<string>()).ViolationsOnly);
        Assert.True(CliArgumentsParser.Parse(["--violations-only"]).ViolationsOnly);
    }

    [Fact]
    public void ViolationsOnlyEqualsFormThrows()
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--violations-only=x"]));
    }

    private const string MixedSource = """
        namespace Demo;
        class Sample {
            int Calm(bool a) {
                if (a) {
                    return 1;
                }
                return 0;
            }
            int Stormy(bool a, bool b) {
                if (a) {
                    return 1;
                }
                if (b) {
                    return 2;
                }
                return 0;
            }
            int Ghost() {
                return 3;
            }
        }
        """;

    private const string OrderedSource = """
        namespace Demo;
        class Sample {
            int Calm(bool a) {
                if (a) {
                    return 1;
                }
                return 0;
            }
            int Stormy(bool a, bool b) {
                if (a) {
                    return 1;
                }
                if (b) {
                    return 2;
                }
                return 0;
            }
            int Monsoon(bool a, bool b, bool c) {
                if (a) {
                    return 1;
                }
                if (b) {
                    return 2;
                }
                if (c) {
                    return 3;
                }
                return 0;
            }
        }
        """;

    private static string CoverageXml(string methodsXml) => """
        <coverage>
          <packages>
            <package name="Demo">
              <classes>
                <class name="Demo/Sample" filename="Sample.cs">
                  <methods>
        """ + methodsXml + """
                  </methods>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;

    private static string FullLines(int from, int to, int hits) =>
        string.Concat(Enumerable.Range(from, to - from + 1)
            .Select(n => $"                      <line number=\"{n}\" hits=\"{hits}\"/>\n"));

    private CoverageRunner RunnerFor(string xml)
    {
        var resultsDir = Path.Combine(_tempDir, "TestResults", "run1");
        return new CoverageRunner(new FakeExecutor((_, _) =>
        {
            Directory.CreateDirectory(resultsDir);
            File.WriteAllText(Path.Combine(resultsDir, "coverage.cobertura.xml"), xml);
            return 0;
        }));
    }

    private string WriteSample(string source)
    {
        var sourceRoot = Path.Combine(_tempDir, "src", "Demo");
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(sourceRoot, "Sample.cs"), source);
        return Path.Combine("src", "Demo", "Sample.cs");
    }

    private readonly string _mixedMethodsXml =
        "            <method name=\"Calm\" signature=\"()V\">\n"
        + "              <lines>\n"
        + FullLines(3, 8, 1)
        + "              </lines>\n"
        + "            </method>\n"
        + "            <method name=\"Stormy\" signature=\"()V\">\n"
        + "              <lines>\n"
        + FullLines(9, 17, 0)
        + "              </lines>\n"
        + "            </method>\n";

    [Fact]
    public void HidesPassingAndNaRowsButKeepsGate()
    {
        var rel = WriteSample(MixedSource);
        using var output = new StringWriter();
        using var err = new StringWriter();

        var exit = new CliApplication(_tempDir, output, err, RunnerFor(CoverageXml(_mixedMethodsXml)))
            .Execute([rel, "--violations-only"]);

        var report = output.ToString();
        Assert.Equal(2, exit);
        Assert.Contains("Stormy", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Calm", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Ghost", report, StringComparison.Ordinal);
        Assert.Contains("CRAP threshold exceeded:", err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyViolatorsPrintsEmptyTextReportWithExitZero()
    {
        var rel = WriteSample(MixedSource);
        using var output = new StringWriter();
        using var err = new StringWriter();

        var exit = new CliApplication(_tempDir, output, err, RunnerFor(CoverageXml(_mixedMethodsXml)))
            .Execute([rel, "--violations-only", "--threshold", "100"]);

        var report = output.ToString();
        Assert.Equal(0, exit);
        Assert.StartsWith("CRAP Report\n", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Stormy", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Calm", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Ghost", report, StringComparison.Ordinal);
        Assert.DoesNotContain("No C# files to analyze.", report, StringComparison.Ordinal);
    }

    [Fact]
    public void TopKeepsWorstViolatorsFirst()
    {
        var rel = WriteSample(OrderedSource);
        var methodsXml =
            "            <method name=\"Calm\" signature=\"()V\">\n"
            + "              <lines>\n"
            + FullLines(3, 8, 1)
            + "              </lines>\n"
            + "            </method>\n"
            + "            <method name=\"Stormy\" signature=\"()V\">\n"
            + "              <lines>\n"
            + FullLines(9, 17, 0)
            + "              </lines>\n"
            + "            </method>\n"
            + "            <method name=\"Monsoon\" signature=\"()V\">\n"
            + "              <lines>\n"
            + FullLines(18, 29, 0)
            + "              </lines>\n"
            + "            </method>\n";
        using var output = new StringWriter();
        using var err = new StringWriter();

        var exit = new CliApplication(_tempDir, output, err, RunnerFor(CoverageXml(methodsXml)))
            .Execute([rel, "--violations-only", "--top", "1"]);

        var report = output.ToString();
        Assert.Equal(2, exit);
        Assert.Contains("Monsoon", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Stormy", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Calm", report, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomThresholdDrivesFilterAndGate()
    {
        var rel = WriteSample(OrderedSource);
        var methodsXml =
            "            <method name=\"Calm\" signature=\"()V\">\n"
            + "              <lines>\n"
            + FullLines(3, 8, 1)
            + "              </lines>\n"
            + "            </method>\n"
            + "            <method name=\"Stormy\" signature=\"()V\">\n"
            + "              <lines>\n"
            + FullLines(9, 17, 0)
            + "              </lines>\n"
            + "            </method>\n"
            + "            <method name=\"Monsoon\" signature=\"()V\">\n"
            + "              <lines>\n"
            + FullLines(18, 29, 0)
            + "              </lines>\n"
            + "            </method>\n";
        var runner = RunnerFor(CoverageXml(methodsXml));

        using (var output = new StringWriter())
        using (var err = new StringWriter())
        {
            var exit = new CliApplication(_tempDir, output, err, runner)
                .Execute([rel, "--violations-only", "--threshold", "15"]);
            var report = output.ToString();
            Assert.Equal(2, exit);
            Assert.Contains("Monsoon", report, StringComparison.Ordinal);
            Assert.DoesNotContain("Stormy", report, StringComparison.Ordinal);
            Assert.Contains("> 15.0", err.ToString(), StringComparison.Ordinal);
        }

        using (var output = new StringWriter())
        using (var err = new StringWriter())
        {
            var exit = new CliApplication(_tempDir, output, err, runner)
                .Execute([rel, "--violations-only", "--threshold", "25"]);
            Assert.Equal(0, exit);
            Assert.DoesNotContain("Monsoon", output.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MinCcDropsLowComplexityBeforeViolationsFilter()
    {
        var rel = WriteSample(OrderedSource);
        var methodsXml =
            "            <method name=\"Calm\" signature=\"()V\">\n"
            + "              <lines>\n"
            + FullLines(3, 8, 1)
            + "              </lines>\n"
            + "            </method>\n"
            + "            <method name=\"Stormy\" signature=\"()V\">\n"
            + "              <lines>\n"
            + FullLines(9, 17, 0)
            + "              </lines>\n"
            + "            </method>\n"
            + "            <method name=\"Monsoon\" signature=\"()V\">\n"
            + "              <lines>\n"
            + FullLines(18, 29, 0)
            + "              </lines>\n"
            + "            </method>\n";
        using var output = new StringWriter();
        using var err = new StringWriter();

        var exit = new CliApplication(_tempDir, output, err, RunnerFor(CoverageXml(methodsXml)))
            .Execute([rel, "--violations-only", "--min-cc", "4"]);

        var report = output.ToString();
        Assert.Equal(2, exit);
        Assert.Contains("Monsoon", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Stormy", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Calm", report, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyViolatorsCsvIsHeaderOnly()
    {
        var rel = WriteSample(MixedSource);
        using var output = new StringWriter();
        using var err = new StringWriter();

        var exit = new CliApplication(_tempDir, output, err, RunnerFor(CoverageXml(_mixedMethodsXml)))
            .Execute([rel, "--violations-only", "--threshold", "100", "--format", "csv"]);

        Assert.Equal(0, exit);
        Assert.Equal("method,class,cc,coverage,crap\n", output.ToString());
    }

    [Fact]
    public void JsonFormatEmitsOnlyViolators()
    {
        var rel = WriteSample(MixedSource);
        using var output = new StringWriter();
        using var err = new StringWriter();

        var exit = new CliApplication(_tempDir, output, err, RunnerFor(CoverageXml(_mixedMethodsXml)))
            .Execute([rel, "--violations-only", "--format", "json"]);

        var report = output.ToString();
        Assert.Equal(2, exit);
        Assert.Contains("\"method\": \"Stormy\"", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Calm", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Ghost", report, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyViolatorsJsonIsEmptyArray()
    {
        var rel = WriteSample(MixedSource);
        using var output = new StringWriter();
        using var err = new StringWriter();

        var exit = new CliApplication(_tempDir, output, err, RunnerFor(CoverageXml(_mixedMethodsXml)))
            .Execute([rel, "--violations-only", "--threshold", "100", "--format", "json"]);

        Assert.Equal(0, exit);
        Assert.Equal("[\n]\n", output.ToString());
    }

    [Fact]
    public void CsvFormatEmitsOnlyViolators()
    {
        var rel = WriteSample(MixedSource);
        using var output = new StringWriter();
        using var err = new StringWriter();

        var exit = new CliApplication(_tempDir, output, err, RunnerFor(CoverageXml(_mixedMethodsXml)))
            .Execute([rel, "--violations-only", "--format", "csv"]);

        var report = output.ToString();
        Assert.Equal(2, exit);
        Assert.StartsWith("method,class,cc,coverage,crap\n", report, StringComparison.Ordinal);
        Assert.Contains("Stormy", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Calm", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Ghost", report, StringComparison.Ordinal);
    }

    [Fact]
    public void OutputFileMatchesFilteredStdout()
    {
        var rel = WriteSample(MixedSource);
        using var output = new StringWriter();
        using var err = new StringWriter();

        var exit = new CliApplication(_tempDir, output, err, RunnerFor(CoverageXml(_mixedMethodsXml)))
            .Execute([rel, "--violations-only", "--output", "report.txt"]);

        Assert.Equal(2, exit);
        var file = Path.Combine(_tempDir, "report.txt");
        Assert.True(File.Exists(file));
        Assert.Equal(output.ToString(), File.ReadAllText(file));
        Assert.DoesNotContain("Calm", File.ReadAllText(file), StringComparison.Ordinal);
    }
}