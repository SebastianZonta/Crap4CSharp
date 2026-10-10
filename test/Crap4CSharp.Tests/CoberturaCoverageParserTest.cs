using Crap4CSharp;

namespace Crap4CSharp.Tests;

// Mirror of crap4java.JacocoCoverageParserTest adapted to Cobertura XML
// (coverlet): per <method>, covered = lines with hits > 0, missed = hits == 0,
// key "class#method:minLine".
public sealed class CoberturaCoverageParserTest
{
    [Fact]
    public void ParsesCoverageByClassAndMethod()
    {
        string path = WriteTempXml("""
            <coverage>
              <packages>
                <package name="demo">
                  <classes>
                    <class name="demo/Sample" filename="Sample.cs">
                      <methods>
                        <method name="Alpha" signature="()V">
                          <lines>
                            <line number="10" hits="5" branch="false" />
                            <line number="11" hits="3" branch="false" />
                            <line number="12" hits="0" branch="false" />
                          </lines>
                        </method>
                        <method name="Beta" signature="()V">
                          <lines>
                            <line number="20" hits="0" branch="false" />
                          </lines>
                        </method>
                      </methods>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var result = CoberturaCoverageParser.Parse(path);

        Assert.Equal((1, 2), result["demo.Sample#Alpha:10"]);
        Assert.Equal((1, 0), result["demo.Sample#Beta:20"]);
    }

    [Fact]
    public void NormalizesClassNameSeparators()
    {
        string path = WriteTempXml("""
            <coverage>
              <packages>
                <package name="demo">
                  <classes>
                    <class name="demo\Sample" filename="Sample.cs">
                      <methods>
                        <method name="Alpha" signature="()V">
                          <lines>
                            <line number="7" hits="1" branch="false" />
                          </lines>
                        </method>
                      </methods>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var result = CoberturaCoverageParser.Parse(path);

        Assert.Equal((0, 1), result["demo.Sample#Alpha:7"]);
    }

    [Fact]
    public void ParsesInvalidHitsAndLineNumbersAsZero()
    {
        string path = WriteTempXml("""
            <coverage>
              <packages>
                <package name="demo">
                  <classes>
                    <class name="demo/Sample" filename="Sample.cs">
                      <methods>
                        <method name="Alpha" signature="()V">
                          <lines>
                            <line number="oops" hits="oops" branch="false" />
                          </lines>
                        </method>
                      </methods>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var result = CoberturaCoverageParser.Parse(path);

        // Invalid hits -> 0 -> missed; invalid line number -> 0 (mirrors Java parseInt).
        Assert.Equal((1, 0), result["demo.Sample#Alpha:0"]);
    }

    [Fact]
    public void NullOrMissingXmlReturnsEmptyWithoutThrowing()
    {
        Assert.Empty(CoberturaCoverageParser.Parse(null));
        Assert.Empty(CoberturaCoverageParser.Parse(string.Empty));
        Assert.Empty(CoberturaCoverageParser.Parse(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xml")));
    }

    [Fact]
    public void ConfiguresSecureReaderSettings()
    {
        var settings = CoberturaCoverageParser.NewSecureReaderSettings();

        Assert.Equal(System.Xml.DtdProcessing.Prohibit, settings.DtdProcessing);
        // XmlResolver is set-only in .NET (no getter, unlike Java's factory
        // features); the null-resolver behavior is covered by RejectsDoctype.
    }

    [Fact]
    public void RejectsDoctype()
    {
        string path = WriteTempXml("""
            <!DOCTYPE coverage PUBLIC "-//Crap4CSharp//DTD Coverage//EN" "coverage.dtd">
            <coverage>
              <packages />
            </coverage>
            """);

        Assert.Throws<InvalidOperationException>(() => CoberturaCoverageParser.Parse(path));
    }

    private static string WriteTempXml(string xml)
    {
        string path = Path.Combine(Path.GetTempPath(), "cobertura-" + Guid.NewGuid().ToString("N") + ".xml");
        File.WriteAllText(path, xml);
        return path;
    }
}
