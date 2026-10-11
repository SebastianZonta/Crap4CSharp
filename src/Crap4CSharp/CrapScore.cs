namespace Crap4CSharp;

internal static class CrapScore
{
    // Ascending key order (null smallest), so descending puts nulls last.
    // Shared by the analyzer sort, the CLI sort, and the report formatters.
    internal static readonly Comparer<double?> CrapKeyComparer = Comparer<double?>.Create(
        static (a, b) => (a, b) switch
        {
            (null, null) => 0,
            (null, _) => -1,
            (_, null) => 1,
            _ => a.Value!.CompareTo(b.Value!),
        });

    internal static int CompareByCrapDesc(MethodMetrics a, MethodMetrics b) =>
        CrapKeyComparer.Compare(b.CrapScore, a.CrapScore);

    internal static double MaxCrap(this IEnumerable<MethodMetrics> metrics)
    {
        var max = 0.0;
        foreach (var metric in metrics)
        {
            if (metric.CrapScore is not null)
            {
                max = Math.Max(max, metric.CrapScore.Value);
            }
        }
        return max;
    }

    public static double? Calculate(int complexity, double? coveragePercent)
    {
        if (coveragePercent is null)
        {
            return null;
        }

        double cc = complexity;
        var uncovered = 1.0 - (coveragePercent.Value / 100.0);
        return (cc * cc * uncovered * uncovered * uncovered) + cc;
    }
}