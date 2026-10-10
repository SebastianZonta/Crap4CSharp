namespace Crap4CSharp;

internal static class CrapAnalyzer
{
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
                double? crap = CrapScore.Calculate(method.Complexity, coverage);
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
        ReadOnlySpan<char> ns = ScanNamespace(source);
        if (ns.IsEmpty)
        {
            return new string(simpleName);
        }
        return string.Create(null, $"{ns}.{simpleName}");
    }

    // Forward span scan replacing the old whole-source namespace regex
    // `(?m)^\s*namespace\s+([A-Za-z_][\w.]*)\s*[;{]`: per line, skip leading
    // whitespace, match the `namespace` keyword, then the dotted identifier.
    private static ReadOnlySpan<char> ScanNamespace(ReadOnlySpan<char> source)
    {
        int i = 0;
        while (i < source.Length)
        {
            int lineEnd = source.Slice(i).IndexOf('\n');
            ReadOnlySpan<char> line = lineEnd < 0 ? source.Slice(i) : source.Slice(i, lineEnd);
            if (line.Length > 0 && line[line.Length - 1] == '\r')
            {
                line = line.Slice(0, line.Length - 1);
            }
            int pos = 0;
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
                int nameStart = pos;
                while (pos < line.Length && (char.IsLetterOrDigit(line[pos]) || line[pos] == '_' || line[pos] == '.'))
                {
                    pos++;
                }
                if (pos > nameStart && line[nameStart] != '.' && line[pos - 1] != '.'
                    && (line[nameStart] == '_' || char.IsLetter(line[nameStart])))
                {
                    int end = pos;
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
        string key = className.CoverageKey(methodName, line);
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
        string prefix = className.MethodPrefix(methodName);
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

    public static int ParseTrailingLine(this string key) => ParseTrailingLine(key.AsSpan());

    public static int ParseTrailingLine(this ReadOnlySpan<char> span)
    {
        int separator = span.LastIndexOf(ToolConstants.CoverageKeyLineSeparator);
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
            ReadOnlySpan<char> key = entry.Key;
            int separator = key.LastIndexOf(ToolConstants.CoverageKeyLineSeparator);
            if (separator < 0)
            {
                continue;
            }
            string methodKey = new(key[..separator]);
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
        string methodKey = className.MethodKey(methodName);
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
