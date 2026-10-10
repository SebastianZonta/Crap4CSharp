using System.Text.RegularExpressions;

namespace Crap4CSharp;

internal static partial class CrapAnalyzer
{
    [System.Text.RegularExpressions.GeneratedRegex(@"(?m)^\s*namespace\s+([A-Za-z_][\w.]*)\s*[;{]", System.Text.RegularExpressions.RegexOptions.Compiled)]
    private static partial System.Text.RegularExpressions.Regex NamespacePattern();

    public static IReadOnlyList<MethodMetrics> Analyze(
        string projectRoot, IReadOnlyList<string> files, string? coberturaXml)
    {
        var coverageMap = ToCoverageData(CoberturaCoverageParser.Parse(coberturaXml));
        var coverageIndex = BuildMethodIndex(coverageMap);
        List<MethodMetrics> metrics = [];

        foreach (string file in files)
        {
            if (!File.Exists(file))
            {
                continue;
            }
            string source = File.ReadAllText(file);
            string className = ClassNameFromSource(file, source);
            foreach (MethodDescriptor method in CSharpMethodParser.Parse(source))
            {
                double? coverage = LookupIndexed(coverageIndex, className, method.Name, method.StartLine);
                // CRAP formula inlined in the hot loop (same math as
                // CrapScore.Calculate, which stays as the tested seam).
                double? crap = null;
                if (coverage is not null)
                {
                    double cc = method.Complexity;
                    double uncovered = 1.0 - (coverage.Value / 100.0);
                    crap = (cc * cc * uncovered * uncovered * uncovered) + cc;
                }
                metrics.Add(new MethodMetrics(method.Name, className, method.Complexity, coverage, crap));
            }
        }

        metrics.Sort((a, b) =>
            (a.CrapScore, b.CrapScore) switch
            {
                (null, null) => 0,
                (null, _) => 1,
                (_, null) => -1,
                (var x, var y) => y.Value.CompareTo(x.Value),
            });
        return metrics.AsReadOnly();
    }

    public static string ClassNameFromSource(string file, string source)
    {
        string simpleName = Path.GetFileName(file);
        if (simpleName.EndsWith(".cs", StringComparison.Ordinal))
        {
            simpleName = simpleName[..^".cs".Length];
        }
        Match match = NamespacePattern().Match(source);
        if (!match.Success)
        {
            return simpleName;
        }
        return match.Groups[1].Value + "." + simpleName;
    }

    public static double? LookupCoverage(
        IDictionary<string, CoverageData> coverageMap,
        string className, string methodName, int line)
    {
        double? exact = ExactCoverage(coverageMap, className, methodName, line);
        if (exact is not null)
        {
            return exact;
        }

        CoverageData? nearest = NearestCoverage(coverageMap, className, methodName, line);
        return nearest?.CoveragePercent();
    }

    public static double? ExactCoverage(
        IDictionary<string, CoverageData> coverageMap,
        string className, string methodName, int line)
    {
        string key = className + "#" + methodName + ":" + line;
        if (!coverageMap.TryGetValue(key, out CoverageData exact))
        {
            return null;
        }
        return exact.CoveragePercent();
    }

    public static CoverageData? NearestCoverage(
        IDictionary<string, CoverageData> coverageMap,
        string className, string methodName, int line)
    {
        string prefix = className + "#" + methodName + ":";
        CoverageData? nearest = null;
        int nearestDistance = int.MaxValue;
        foreach (var entry in coverageMap)
        {
            if (!entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }
            int distance = Math.Abs(ParseTrailingLine(entry.Key) - line);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = entry.Value;
            }
        }
        return nearest;
    }

    public static int ParseTrailingLine(string key)
    {
        ReadOnlySpan<char> span = key.AsSpan();
        int separator = span.LastIndexOf(':');
        if (separator < 0)
        {
            return int.MaxValue;
        }
        ReadOnlySpan<char> linePart = span[(separator + 1)..];
        if (linePart.Length == 0)
        {
            return int.MaxValue;
        }
        if (int.TryParse(linePart, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out int line))
        {
            return line;
        }
        return int.MaxValue;
    }

    private static Dictionary<string, List<(int Line, CoverageData Data)>> BuildMethodIndex(
        IDictionary<string, CoverageData> coverageMap)
    {
        var index = new Dictionary<string, List<(int Line, CoverageData Data)>>(StringComparer.Ordinal);
        foreach (var entry in coverageMap)
        {
            string key = entry.Key;
            int separator = key.LastIndexOf(':');
            if (separator < 0)
            {
                continue;
            }
            string methodKey = key.Substring(0, separator);
            int line = ParseTrailingLine(key);
            if (!index.TryGetValue(methodKey, out List<(int Line, CoverageData Data)>? entries))
            {
                entries = [];
                index[methodKey] = entries;
            }
            entries.Add((line, entry.Value));
        }
        return index;
    }

    private static double? LookupIndexed(
        Dictionary<string, List<(int Line, CoverageData Data)>> index,
        string className, string methodName, int line)
    {
        string methodKey = className + "#" + methodName;
        if (!index.TryGetValue(methodKey, out List<(int Line, CoverageData Data)>? entries))
        {
            return null;
        }
        foreach (var entry in entries)
        {
            if (entry.Line == line)
            {
                return entry.Data.CoveragePercent();
            }
        }
        CoverageData? nearest = null;
        int nearestDistance = int.MaxValue;
        foreach (var entry in entries)
        {
            int distance = Math.Abs(entry.Line - line);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = entry.Data;
            }
        }
        return nearest?.CoveragePercent();
    }

    private static Dictionary<string, CoverageData> ToCoverageData(
        IReadOnlyDictionary<string, (int Missed, int Covered)> parsed)
    {
        var map = new Dictionary<string, CoverageData>(StringComparer.Ordinal);
        foreach (var entry in parsed)
        {
            map[entry.Key] = new CoverageData(entry.Value.Missed, entry.Value.Covered);
        }
        return map;
    }
}
