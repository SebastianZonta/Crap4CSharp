using System.Globalization;

namespace Crap4CSharp;

internal static class ReportFormatter
{
    private static readonly string _header = string.Create(
        CultureInfo.InvariantCulture, $"{"Method",-30} {"Class",-35} {"CC",4} {"Cov%",7} {"CRAP",8}");
    private static readonly string _separator = new('-', _header.Length);
    private const int EstimatedRowLength = 90;

    public static string Format(IReadOnlyList<MethodMetrics> entries) => FormatText(entries);

    public static string FormatText(IReadOnlyList<MethodMetrics> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var order = SortedIndexes(entries);

        var builder = new System.Text.StringBuilder((entries.Count + 4) * EstimatedRowLength);
        builder.Append(ToolConstants.ReportTitle);
        builder.Append(_header).Append('\n');
        builder.Append(_separator).Append('\n');

        foreach (var index in order)
        {
            var entry = entries[index];
            var coverage = FormatCoverage(entry.CoveragePercent);
            var crap = FormatCrap(entry.CrapScore);
            builder.Append(CultureInfo.InvariantCulture, $"{Truncate(entry.MethodName, MethodColumnWidth),-30} {Truncate(entry.ClassName, ClassColumnWidth),-35} {entry.Complexity,4} {coverage,7} {crap,8}\n");
        }

        return builder.ToString();
    }

    private const int MethodColumnWidth = 30;
    private const int ClassColumnWidth = 35;

    private static string Truncate(string value, int width) =>
        value.Length <= width ? value : value[..width];

    private static string FormatCoverage(double? coverage)
    {
        if (coverage is null)
        {
            return "  N/A ";
        }

        Span<char> buffer = stackalloc char[32];
        coverage.Value.TryFormat(buffer, out var written, "F1", CultureInfo.InvariantCulture);
        return string.Create(null, $"{buffer[..written],5}%");
    }

    private static string FormatCrap(double? score)
    {
        if (score is null)
        {
            return "     N/A";
        }

        Span<char> buffer = stackalloc char[32];
        score.Value.TryFormat(buffer, out var written, "F1", CultureInfo.InvariantCulture);
        return string.Create(null, $"{buffer[..written],8}");
    }

    public static string FormatAs(IReadOnlyList<MethodMetrics> entries, string format) =>
        format switch
        {
            "json" => FormatJson(entries),
            "csv" => FormatCsv(entries),
            _ => FormatText(entries),
        };

    public static string FormatJson(IReadOnlyList<MethodMetrics> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var order = SortedIndexes(entries);
        var builder = new System.Text.StringBuilder((entries.Count + 2) * EstimatedRowLength);
        builder.Append("[\n");
        for (var k = 0; k < order.Length; k++)
        {
            var e = entries[order[k]];
            builder.Append("  {\"method\": \"");
            AppendJsonEscaped(builder, e.MethodName);
            builder.Append("\", \"class\": \"");
            AppendJsonEscaped(builder, e.ClassName);
            builder.Append(CultureInfo.InvariantCulture, $"\", \"cc\": {e.Complexity}, \"coverage\": ");
            AppendNullableNumber(builder, e.CoveragePercent);
            builder.Append(", \"crap\": ");
            AppendNullableNumber(builder, e.CrapScore);
            builder.Append('}');
            builder.Append(k + 1 < order.Length ? ",\n" : "\n");
        }
        builder.Append("]\n");
        return builder.ToString();
    }

    public static string FormatCsv(IReadOnlyList<MethodMetrics> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var order = SortedIndexes(entries);
        var builder = new System.Text.StringBuilder((entries.Count + 1) * EstimatedRowLength);
        builder.Append("method,class,cc,coverage,crap\n");
        foreach (var index in order)
        {
            var e = entries[index];
            AppendCsvEscaped(builder, e.MethodName);
            builder.Append(',');
            AppendCsvEscaped(builder, e.ClassName);
            builder.Append(',');
            builder.Append(CultureInfo.InvariantCulture, $"{e.Complexity},");
            AppendNullableNumber(builder, e.CoveragePercent, emptyForNull: true);
            builder.Append(',');
            AppendNullableNumber(builder, e.CrapScore, emptyForNull: true);
            builder.Append('\n');
        }
        return builder.ToString();
    }

    private static int[] SortedIndexes(IReadOnlyList<MethodMetrics> entries)
    {
        var order = new int[entries.Count];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }
        MemoryExtensions.Sort(order.AsSpan(), (a, b) =>
        {
            var byCrap = CrapScore.CrapKeyComparer.Compare(entries[b].CrapScore, entries[a].CrapScore);
            return byCrap != 0 ? byCrap : a.CompareTo(b);
        });
        return order;
    }

    private static void AppendNullableNumber(System.Text.StringBuilder builder, double? value, bool emptyForNull = false)
    {
        if (value is null)
        {
            if (!emptyForNull)
            {
                builder.Append("null");
            }
            return;
        }
        Span<char> buffer = stackalloc char[32];
        value.Value.TryFormat(buffer, out var written, "F1", CultureInfo.InvariantCulture);
        builder.Append(buffer[..written]);
    }

    private static void AppendJsonEscaped(System.Text.StringBuilder builder, ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case var control when control < 0x20:
                    builder.Append("\\u");
                    builder.Append(((int)control).ToString("x4", CultureInfo.InvariantCulture));
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }
    }

    private static void AppendCsvEscaped(System.Text.StringBuilder builder, ReadOnlySpan<char> value)
    {
        var needsQuotes = false;
        foreach (var c in value)
        {
            if (c == ',' || c == '"' || c == '\n' || c == '\r')
            {
                needsQuotes = true;
                break;
            }
        }
        if (!needsQuotes)
        {
            builder.Append(value);
            return;
        }
        builder.Append('"');
        foreach (var c in value)
        {
            if (c == '"')
            {
                builder.Append("\"\"");
            }
            else
            {
                builder.Append(c);
            }
        }
        builder.Append('"');
    }
}