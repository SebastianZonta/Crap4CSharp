using System.Globalization;

namespace Crap4CSharp;

internal static class ReportFormatter
{
    private static readonly string Header = string.Create(
        CultureInfo.InvariantCulture, $"{"Method",-30} {"Class",-35} {"CC",4} {"Cov%",7} {"CRAP",8}");
    private static readonly string Separator = new('-', Header.Length);
    // Ascending key order (null smallest), so descending puts nulls last.
    private static readonly Comparer<double?> CrapKeyComparer = Comparer<double?>.Create(
        static (a, b) => (a, b) switch
        {
            (null, null) => 0,
            (null, _) => -1,
            (_, null) => 1,
            _ => a.Value!.CompareTo(b.Value!),
        });
    private const int EstimatedRowLength = 90;

    public static string Format(IReadOnlyList<MethodMetrics> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        IEnumerable<MethodMetrics> sorted =
            entries.OrderByDescending(static e => e.CrapScore, CrapKeyComparer);

        var builder = new System.Text.StringBuilder((entries.Count + 4) * EstimatedRowLength);
        builder.Append("CRAP Report\n");
        builder.Append("===========\n");
        builder.Append(Header).Append('\n');
        builder.Append(Separator).Append('\n');

        foreach (var entry in sorted)
        {
            string coverage = FormatCoverage(entry.CoveragePercent);
            string crap = FormatCrap(entry.CrapScore);
            builder.Append(CultureInfo.InvariantCulture, $"{entry.MethodName,-30} {entry.ClassName,-35} {entry.Complexity,4} {coverage,7} {crap,8}\n");
        }

        return builder.ToString();
    }

    private static string FormatCoverage(double? coverage)
    {
        if (coverage is null)
        {
            return "  N/A ";
        }

        return string.Create(CultureInfo.InvariantCulture, $"{coverage.Value,5:F1}%");
    }

    private static string FormatCrap(double? score)
    {
        if (score is null)
        {
            return "     N/A";
        }

        return string.Create(CultureInfo.InvariantCulture, $"{score.Value,8:F1}");
    }
}
