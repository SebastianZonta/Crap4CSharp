namespace Crap4CSharp.Tests;

public sealed class CrapAnalyzerTest : IDisposable
{
    private static readonly string[] ExpectedMethodOrder = ["Alpha", "Beta", "Gamma"];
    private readonly string _tempDir = Directory.CreateTempSubdirectory("crap4csharp-test-").FullName;

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

    [Fact]
    public void ComputesScoresForChangedFiles()
    {
        string sourceRoot = Path.Combine(_tempDir, "src", "Sample");
        Directory.CreateDirectory(sourceRoot);
        string source = Path.Combine(sourceRoot, "Sample.cs");
        File.WriteAllText(source, """
            namespace Demo;
            class Sample {
                int Alpha(bool a) {
                    if (a) {
                        return 1;
                    }
                    return 0;
                }
            }
            """);

        string cobertura = Path.Combine(_tempDir, "coverage.cobertura.xml");
        File.WriteAllText(cobertura, """
            <coverage>
              <packages>
                <package name="Demo">
                  <classes>
                    <class name="Demo/Sample" filename="Sample.cs">
                      <methods>
                        <method name="Alpha" signature="()V">
                          <lines>
                            <line number="3" hits="3"/>
                            <line number="4" hits="0"/>
                            <line number="5" hits="3"/>
                            <line number="8" hits="3"/>
                          </lines>
                        </method>
                      </methods>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var result = CrapAnalyzer.Analyze(_tempDir, new[] { source }, cobertura);

        Assert.Single(result);
        MethodMetrics metric = result[0];
        Assert.Equal("Alpha", metric.MethodName);
        Assert.Equal("Demo.Sample", metric.ClassName);
        Assert.Equal(2, metric.Complexity);
        Assert.Equal(75.0, metric.CoveragePercent!.Value, 0.001);
        Assert.Equal(2.0625, metric.CrapScore!.Value, 0.00001);
    }

    [Fact]
    public void SkipsMissingFilesWithoutThrowing()
    {
        string missing = Path.Combine(_tempDir, "src", "Gone.cs");

        var result = CrapAnalyzer.Analyze(_tempDir, new[] { missing }, null);

        Assert.Empty(result);
    }

    [Fact]
    public void SortsMixedKnownAndUnknownScoresWithUnknownsLast()
    {
        string sourceRoot = Path.Combine(_tempDir, "src", "Sample");
        Directory.CreateDirectory(sourceRoot);
        string source = Path.Combine(sourceRoot, "Sample.cs");
        File.WriteAllText(source, """
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

        // Coverage only for the Known/Plain methods, at lines that do not
        // exactly match the method starts (exercises nearest-line fallback).
        string cobertura = Path.Combine(_tempDir, "coverage.cobertura.xml");
        File.WriteAllText(cobertura, """
            <coverage>
              <packages>
                <package name="Demo">
                  <classes>
                    <class name="Demo/Sample" filename="Sample.cs">
                      <methods>
                        <method name="Known" signature="()V">
                          <lines>
                            <line number="5" hits="2"/>
                            <line number="9" hits="2"/>
                          </lines>
                        </method>
                        <method name="Plain" signature="()V">
                          <lines>
                            <line number="17" hits="0"/>
                            <line number="21" hits="0"/>
                          </lines>
                        </method>
                      </methods>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var result = CrapAnalyzer.Analyze(_tempDir, new[] { source }, cobertura);

        // Scored methods first (higher CRAP first), unknown scores last.
        Assert.Equal(["Plain", "Known", "Mystery", "Other"],
            result.Select(m => m.MethodName).ToList());
    }

    [Fact]
    public void NearestCoverageIgnoresOtherMethods()
    {
        var coverageMap = new Dictionary<string, CoverageData>
        {
            ["Demo.Sample#Beta:10"] = new CoverageData(0, 8),
            ["Demo.Sample#Alpha:10"] = new CoverageData(1, 3),
        };

        double? coverage = CrapAnalyzer.LookupCoverage(coverageMap, "Demo.Sample", "Alpha", 12);

        Assert.Equal(75.0, coverage!.Value, 0.001);
    }

    [Fact]
    public void UsesSimpleClassNameWhenSourceHasNoNamespace()
    {
        string className = CrapAnalyzer.ClassNameFromSource(
            Path.Combine("src", "Sample.cs"),
            """
            class Sample {
            }
            """);

        Assert.Equal("Sample", className);
    }

    [Fact]
    public void LookupCoveragePrefersExactLineBeforeNearestMatch()
    {
        var coverageMap = new Dictionary<string, CoverageData>
        {
            ["Demo.Sample#Alpha:10"] = new CoverageData(1, 3),
            ["Demo.Sample#Alpha:12"] = new CoverageData(0, 8),
        };

        double? coverage = CrapAnalyzer.LookupCoverage(coverageMap, "Demo.Sample", "Alpha", 10);

        Assert.Equal(75.0, coverage!.Value, 0.001);
    }

    [Fact]
    public void LookupCoverageFallsBackToNearestLineWithinMethod()
    {
        var coverageMap = new Dictionary<string, CoverageData>
        {
            ["Demo.Sample#Alpha:10"] = new CoverageData(1, 3),
            ["Demo.Sample#Alpha:15"] = new CoverageData(0, 8),
        };

        double? coverage = CrapAnalyzer.LookupCoverage(coverageMap, "Demo.Sample", "Alpha", 13);

        Assert.Equal(100.0, coverage!.Value, 0.001);
    }

    [Fact]
    public void NearestCoverageKeepsFirstEntryWhenDistancesTie()
    {
        var coverageMap = new Dictionary<string, CoverageData>
        {
            ["Demo.Sample#Alpha:10"] = new CoverageData(1, 3),
            ["Demo.Sample#Alpha:14"] = new CoverageData(0, 8),
        };

        CoverageData? nearest = CrapAnalyzer.NearestCoverage(coverageMap, "Demo.Sample", "Alpha", 12);

        Assert.Equal(75.0, nearest!.Value.CoveragePercent(), 0.001);
    }

    [Fact]
    public void LookupCoverageReturnsNullWhenMethodHasNoCoverageEntries()
    {
        double? coverage = CrapAnalyzer.LookupCoverage(
            new Dictionary<string, CoverageData>(), "Demo.Sample", "Alpha", 10);

        Assert.Null(coverage);
    }

    [Fact]
    public void ParseTrailingLineReturnsMaxValueForMalformedKeys()
    {
        Assert.Equal(int.MaxValue, CrapAnalyzer.ParseTrailingLine("Demo.Sample#Alpha"));
        Assert.Equal(int.MaxValue, CrapAnalyzer.ParseTrailingLine("Demo.Sample#Alpha:"));
        Assert.Equal(int.MaxValue, CrapAnalyzer.ParseTrailingLine("Demo.Sample#Alpha:oops"));
    }

    [Fact]
    public void ParseTrailingLineReturnsParsedLineNumberForValidKey()
    {
        Assert.Equal(10, CrapAnalyzer.ParseTrailingLine("Demo.Sample#Alpha:10"));
    }

    [Fact]
    public void ParseTrailingLineAcceptsLeadingSeparator()
    {
        Assert.Equal(10, CrapAnalyzer.ParseTrailingLine(":10"));
    }

    [Fact]
    public void SortsMetricsByScoreDescendingWithNullsLast()
    {
        string sourceRoot = Path.Combine(_tempDir, "src", "Sample");
        Directory.CreateDirectory(sourceRoot);
        string source = Path.Combine(sourceRoot, "Sample.cs");
        File.WriteAllText(source, """
            namespace Demo;

            class Sample {
                int Alpha(bool a) {
                    if (a) {
                        return 1;
                    }
                    return 0;
                }

                int Beta() {
                    return 1;
                }

                int Gamma() {
                    return 2;
                }
            }
            """);

        string cobertura = Path.Combine(_tempDir, "coverage.cobertura.xml");
        File.WriteAllText(cobertura, """
            <coverage>
              <packages>
                <package name="Demo">
                  <classes>
                    <class name="Demo/Sample" filename="Sample.cs">
                      <methods>
                        <method name="Alpha" signature="()V">
                          <lines>
                            <line number="4" hits="0"/>
                            <line number="5" hits="0"/>
                            <line number="8" hits="0"/>
                            <line number="10" hits="0"/>
                          </lines>
                        </method>
                        <method name="Beta" signature="()V">
                          <lines>
                            <line number="13" hits="4"/>
                            <line number="14" hits="4"/>
                          </lines>
                        </method>
                      </methods>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var result = CrapAnalyzer.Analyze(_tempDir, new[] { source }, cobertura);

        Assert.Equal(ExpectedMethodOrder,
            result.Select(m => m.MethodName).ToList());
    }
}
