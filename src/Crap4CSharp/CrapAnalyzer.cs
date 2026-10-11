namespace Crap4CSharp;

internal static class CrapAnalyzer
{
    public static IReadOnlyList<MethodMetrics> Analyze(
        string projectRoot, IReadOnlyList<string> files, string? coberturaXml)
    {
        var coverageMap = ToCoverageData(CoberturaCoverageParser.Parse(coberturaXml));
        var coverageIndex = BuildMethodIndex(coverageMap);
        List<MethodMetrics> metrics = [];

        foreach (var file in files)
        {
            if (!File.Exists(file))
            {
                continue;
            }
            var source = File.ReadAllText(file);
            var className = ClassNameFromSource(file, source);
            foreach (var method in CSharpMethodParser.Parse(source))
            {
                var coverage = LookupIndexed(coverageIndex, className, method.Name, method.StartLine);
                var crap = CrapScore.Calculate(method.Complexity, coverage);
                metrics.Add(new MethodMetrics(method.Name, className, method.Complexity, coverage, crap));
            }
        }

        metrics.Sort(CrapScore.CompareByCrapDesc);
        return metrics.AsReadOnly();
    }

    public static string ClassNameFromSource(string file, string source)
    {
        ReadOnlySpan<char> simpleName = Path.GetFileName(file);
        ReadOnlySpan<char> ext = ToolConstants.CSharpExtension;
        if (simpleName.EndsWith(ext, StringComparison.Ordinal))
        {
            simpleName = simpleName[..^ext.Length];
        }
        var ns = ScanNamespace(source);
        if (ns.IsEmpty)
        {
            return new string(simpleName);
        }
        return string.Create(null, $"{ns}.{simpleName}");
    }

    private static ReadOnlySpan<char> ScanNamespace(ReadOnlySpan<char> source)
    {
        var i = 0;
        while (i < source.Length)
        {
            var lineEnd = source.Slice(i).IndexOf('\n');
            var line = lineEnd < 0 ? source.Slice(i) : source.Slice(i, lineEnd);
            if (line.Length > 0 && line[line.Length - 1] == '\r')
            {
                line = line.Slice(0, line.Length - 1);
            }
            var pos = 0;
            while (pos < line.Length && char.IsWhiteSpace(line[pos]))
            {
                pos++;
            }
            ReadOnlySpan<char> keyword = "namespace";
            if (line.Length - pos > keyword.Length
                && line.Slice(pos, keyword.Length).SequenceEqual(keyword)
                && char.IsWhiteSpace(line[pos + keyword.Length]))
            {
                pos += keyword.Length;
                while (pos < line.Length && char.IsWhiteSpace(line[pos]))
                {
                    pos++;
                }
                var nameStart = pos;
                while (pos < line.Length && (char.IsLetterOrDigit(line[pos]) || line[pos] == '_' || line[pos] == '.'))
                {
                    pos++;
                }
                if (pos > nameStart && line[nameStart] != '.' && line[pos - 1] != '.'
                    && (line[nameStart] == '_' || char.IsLetter(line[nameStart])))
                {
                    var end = pos;
                    while (end < line.Length && char.IsWhiteSpace(line[end]))
                    {
                        end++;
                    }
                    if (end < line.Length && (line[end] == ';' || line[end] == '{'))
                    {
                        return line.Slice(nameStart, pos - nameStart);
                    }
                }
            }
            if (lineEnd < 0)
            {
                break;
            }
            i += lineEnd + 1;
        }
        return ReadOnlySpan<char>.Empty;
    }

    public static double? LookupCoverage(
        IDictionary<string, CoverageData> coverageMap,
        string className, string methodName, int line)
    {
        var exact = ExactCoverage(coverageMap, className, methodName, line);
        if (exact is not null)
        {
            return exact;
        }

        var nearest = NearestCoverage(coverageMap, className, methodName, line);
        return nearest?.CoveragePercent();
    }

    public static double? ExactCoverage(
        IDictionary<string, CoverageData> coverageMap,
        string className, string methodName, int line)
    {
        var key = className.CoverageKey(methodName, line);
        if (!coverageMap.TryGetValue(key, out var exact))
        {
            return null;
        }
        return exact.CoveragePercent();
    }

    public static CoverageData? NearestCoverage(
        IDictionary<string, CoverageData> coverageMap,
        string className, string methodName, int line)
    {
        var prefix = className.MethodPrefix(methodName);
        CoverageData? nearest = null;
        var nearestDistance = int.MaxValue;
        foreach (var entry in coverageMap)
        {
            if (!entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }
            var distance = Math.Abs(ParseTrailingLine(entry.Key) - line);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = entry.Value;
            }
        }
        return nearest;
    }

    public static int ParseTrailingLine(this string key) => ParseTrailingLine(key.AsSpan());

    public static int ParseTrailingLine(this ReadOnlySpan<char> span)
    {
        var separator = span.LastIndexOf(ToolConstants.CoverageKeyLineSeparator);
        if (separator < 0)
        {
            return int.MaxValue;
        }
        var linePart = span[(separator + 1)..];
        if (linePart.Length == 0)
        {
            return int.MaxValue;
        }
        if (int.TryParse(linePart, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var line))
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
            ReadOnlySpan<char> key = entry.Key;
            var separator = key.LastIndexOf(ToolConstants.CoverageKeyLineSeparator);
            if (separator < 0)
            {
                continue;
            }
            string methodKey = new(key[..separator]);
            var line = ParseTrailingLine(key);
            if (!index.TryGetValue(methodKey, out var entries))
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
        var methodKey = className.MethodKey(methodName);
        if (!index.TryGetValue(methodKey, out var entries))
        {
            return null;
        }
        foreach (var (Line, Data) in entries)
        {
            if (Line == line)
            {
                return Data.CoveragePercent();
            }
        }
        CoverageData? nearest = null;
        var nearestDistance = int.MaxValue;
        foreach (var (Line, Data) in entries)
        {
            var distance = Math.Abs(Line - line);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = Data;
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