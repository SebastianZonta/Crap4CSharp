using System.Globalization;

namespace Crap4CSharp;

public static class ReportFormatter
{
    public static string Format(IReadOnlyList<MethodMetrics> entries)
    {
        var sorted = entries
            .OrderBy(e => e.CrapScore is null)
            .ThenBy(e => e.CrapScore is null ? 0.0 : -e.CrapScore.Value)
            .ToList();

        string header = string.Format(
            CultureInfo.InvariantCulture,
            "{0,-30} {1,-35} {2,4} {3,7} {4,8}",
            "Method", "Class", "CC", "Cov%", "CRAP");
        string separator = new string('-', header.Length);

        var builder = new System.Text.StringBuilder();
        builder.Append("CRAP Report\n");
        builder.Append("===========\n");
        builder.Append(header).Append('\n');
        builder.Append(separator).Append('\n');

        foreach (var entry in sorted)
        {
            string row = string.Format(
                CultureInfo.InvariantCulture,
                "{0,-30} {1,-35} {2,4} {3,7} {4,8}",
                entry.MethodName,
                entry.ClassName,
                entry.Complexity,
                FormatCoverage(entry.CoveragePercent),
                FormatCrap(entry.CrapScore));
            builder.Append(row).Append('\n');
        }

        return builder.ToString();
    }

    private static string FormatCoverage(double? coverage)
    {
        if (coverage is null)
        {
            return "  N/A ";
        }

        return string.Format(CultureInfo.InvariantCulture, "{0,5:F1}%", coverage.Value);
    }

    private static string FormatCrap(double? score)
    {
        if (score is null)
        {
            return "     N/A";
        }

        return string.Format(CultureInfo.InvariantCulture, "{0,8:F1}", score.Value);
    }
}
