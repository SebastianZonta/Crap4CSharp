using System.Globalization;

namespace Crap4CSharp.Tests;

public sealed class ReportFormatterTest
{
    [Fact]
    public void FormatsExactReportWithScoresAndNaValues()
    {
        var scored = new MethodMetrics("foo", "demo.Sample", 3, 85.0, 4.5);
        var unknown = new MethodMetrics("bar", "demo.Sample", 2, null, null);

        string report = ReportFormatter.Format([scored, unknown]);

        string header = string.Format(CultureInfo.InvariantCulture, "{0,-30} {1,-35} {2,4} {3,7} {4,8}", "Method", "Class", "CC", "Cov%", "CRAP");
        string separator = new string('-', header.Length);
        string expected =
            "CRAP Report\n" +
            "===========\n" +
            $"{header}\n" +
            $"{separator}\n" +
            $"{string.Format(CultureInfo.InvariantCulture, "{0,-30} {1,-35} {2,4} {3,7} {4,8}", "foo", "demo.Sample", 3, "85.0%", "4.5")}\n" +
            $"{string.Format(CultureInfo.InvariantCulture, "{0,-30} {1,-35} {2,4} {3,7} {4,8}", "bar", "demo.Sample", 2, "  N/A ", "     N/A")}\n";

        Assert.Equal(expected, report);
    }

    [Fact]
    public void SortsScoredEntriesAheadOfNaEntriesAndHigherScoresFirst()
    {
        var lowerScore = new MethodMetrics("low", "demo.Sample", 2, 100.0, 2.0);
        var unknown = new MethodMetrics("unknown", "demo.Sample", 2, null, null);
        var higherScore = new MethodMetrics("high", "demo.Sample", 5, 10.0, 9.0);

        string report = ReportFormatter.Format([lowerScore, unknown, higherScore]);

        Assert.True(report.IndexOf("high", StringComparison.Ordinal) < report.IndexOf("low", StringComparison.Ordinal));
        Assert.True(report.IndexOf("low", StringComparison.Ordinal) < report.IndexOf("unknown", StringComparison.Ordinal));
    }
}
