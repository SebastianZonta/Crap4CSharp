using System.Globalization;
using System.Text.Json;

namespace Crap4CSharp.Tests;

public sealed class ReportFormatterTest
{
    [Fact]
    public void FormatsExactReportWithScoresAndNaValues()
    {
        var scored = new MethodMetrics("foo", "demo.Sample", 3, 85.0, 4.5);
        var unknown = new MethodMetrics("bar", "demo.Sample", 2, null, null);

        var report = ReportFormatter.Format([scored, unknown]);

        var header = string.Format(CultureInfo.InvariantCulture, "{0,-30} {1,-35} {2,4} {3,7} {4,8}", "Method", "Class", "CC", "Cov%", "CRAP");
        var separator = new string('-', header.Length);
        var expected =
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

        var report = ReportFormatter.Format([lowerScore, unknown, higherScore]);

        Assert.True(report.IndexOf("high", StringComparison.Ordinal) < report.IndexOf("low", StringComparison.Ordinal));
        Assert.True(report.IndexOf("low", StringComparison.Ordinal) < report.IndexOf("unknown", StringComparison.Ordinal));
    }

    [Fact]
    public void FormatsAllUnknownEntriesWithoutThrowing()
    {
        var first = new MethodMetrics("aaa", "demo.Sample", 1, null, null);
        var second = new MethodMetrics("bbb", "demo.Sample", 2, null, null);
        var third = new MethodMetrics("ccc", "demo.Sample", 3, null, null);

        var report = ReportFormatter.Format([first, second, third]);

        Assert.Contains("aaa", report, StringComparison.Ordinal);
        Assert.Contains("bbb", report, StringComparison.Ordinal);
        Assert.Contains("ccc", report, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatDelegatesToFormatText()
    {
        var entries = new List<MethodMetrics>
        {
            new("foo", "demo.Sample", 3, 85.0, 4.5),
            new("bar", "demo.Sample", 2, null, null),
        };

        Assert.Equal(ReportFormatter.FormatText(entries), ReportFormatter.Format(entries));
    }

    [Fact]
    public void FormatAsDispatchesByFormatName()
    {
        var entries = new List<MethodMetrics>
        {
            new("foo", "demo.Sample", 3, 85.0, 4.5),
        };

        Assert.Equal(ReportFormatter.FormatText(entries), ReportFormatter.FormatAs(entries, "text"));
        Assert.Equal(ReportFormatter.FormatJson(entries), ReportFormatter.FormatAs(entries, "json"));
        Assert.Equal(ReportFormatter.FormatCsv(entries), ReportFormatter.FormatAs(entries, "csv"));
    }

    [Fact]
    public void FormatJsonHasArrayShapeWithKeysAndNulls()
    {
        var scored = new MethodMetrics("foo", "demo.Sample", 3, 85.0, 4.5);
        var unknown = new MethodMetrics("bar", "demo.Sample", 2, null, null);

        var json = ReportFormatter.FormatJson([scored, unknown]);

        Assert.StartsWith("[\n", json, StringComparison.Ordinal);
        Assert.EndsWith("]\n", json, StringComparison.Ordinal);
        Assert.Contains("\"method\": \"foo\"", json, StringComparison.Ordinal);
        Assert.Contains("\"class\": \"demo.Sample\"", json, StringComparison.Ordinal);
        Assert.Contains("\"cc\": 3", json, StringComparison.Ordinal);
        Assert.Contains("\"coverage\": 85.0", json, StringComparison.Ordinal);
        Assert.Contains("\"crap\": 4.5", json, StringComparison.Ordinal);
        Assert.Contains("\"coverage\": null", json, StringComparison.Ordinal);
        Assert.Contains("\"crap\": null", json, StringComparison.Ordinal);
        Assert.True(json.IndexOf("foo", StringComparison.Ordinal) < json.IndexOf("bar", StringComparison.Ordinal));
    }

    [Fact]
    public void FormatCsvHasHeaderRowAndEmptyNulls()
    {
        var scored = new MethodMetrics("foo", "demo.Sample", 3, 85.0, 4.5);
        var unknown = new MethodMetrics("bar", "demo.Sample", 2, null, null);

        var csv = ReportFormatter.FormatCsv([scored, unknown]);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("method,class,cc,coverage,crap", lines[0]);
        Assert.Equal(3, lines.Length);
        Assert.Contains("foo,demo.Sample,3,85.0,4.5", csv, StringComparison.Ordinal);
        Assert.Contains("bar,demo.Sample,2,,", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatJsonAndCsvSortWorstFirst()
    {
        var low = new MethodMetrics("low", "demo.Sample", 2, 100.0, 2.0);
        var high = new MethodMetrics("high", "demo.Sample", 5, 10.0, 9.0);

        var json = ReportFormatter.FormatJson([low, high]);
        var csv = ReportFormatter.FormatCsv([low, high]);

        Assert.True(json.IndexOf("high", StringComparison.Ordinal) < json.IndexOf("low", StringComparison.Ordinal));
        Assert.True(csv.IndexOf("high", StringComparison.Ordinal) < csv.IndexOf("low", StringComparison.Ordinal));
    }

    [Fact]
    public void FormatJsonControlCharactersRoundTripThroughStrictParser()
    {
        var entries = new List<MethodMetrics>
        {
            new("we\nird\tmethod\x01", "demo.Sa\"mple\\Klass\r\n", 3, 85.0, 4.5),
        };

        var json = ReportFormatter.FormatJson(entries);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        var row = root[0];
        Assert.Equal("we\nird\tmethod\x01", row.GetProperty("method").GetString());
        Assert.Equal("demo.Sa\"mple\\Klass\r\n", row.GetProperty("class").GetString());
    }

    [Fact]
    public void FormatTextTruncatesLongNamesToColumnWidth()
    {
        var entries = new List<MethodMetrics>
        {
            new(new string('m', 50), new string('c', 60), 3, 85.0, 4.5),
        };

        var report = ReportFormatter.FormatText(entries);
        var lines = report.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var header = lines[2];

        Assert.Equal(5, lines.Length); // title (2) + header + separator + 1 row
        foreach (var line in lines[2..])
        {
            Assert.Equal(header.Length, line.Length);
        }
        Assert.Contains(new string('m', 30), report, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('m', 31), report, StringComparison.Ordinal);
    }
}