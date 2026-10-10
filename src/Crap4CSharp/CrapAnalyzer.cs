using System.Text.RegularExpressions;

namespace Crap4CSharp;

public static class CrapAnalyzer
{
    private static readonly Regex NamespacePattern =
        new(@"(?m)^\s*namespace\s+([A-Za-z_][\w.]*)\s*[;{]", RegexOptions.Compiled);

    public static IReadOnlyList<MethodMetrics> Analyze(
        string projectRoot, IReadOnlyList<string> files, string? coberturaXml)
    {
        var coverageMap = ToCoverageData(CoberturaCoverageParser.Parse(coberturaXml));
        var metrics = new List<MethodMetrics>();

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
                double? coverage = LookupCoverage(coverageMap, className, method.Name, method.StartLine);
                double? crap = CrapScore.Calculate(method.Complexity, coverage);
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
        Match match = NamespacePattern.Match(source);
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
        if (!coverageMap.TryGetValue(key, out CoverageData? exact))
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
        int separator = key.LastIndexOf(':');
        if (separator < 0)
        {
            return int.MaxValue;
        }
        string lineText = key[(separator + 1)..];
        if (lineText.Length == 0)
        {
            return int.MaxValue;
        }
        if (int.TryParse(lineText, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out int line))
        {
            return line;
        }
        return int.MaxValue;
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
