using System.Globalization;
using System.Xml;

namespace Crap4CSharp;

// Mirrors crap4java.JacocoCoverageParser (final class + private ctor + statics)
// adapted to Cobertura XML (coverlet): per <method>, covered = lines with
// hits > 0, missed = lines with hits == 0, key "class#method:minLine".
//
// Coverage value: ticket 07 owns the top-level CoverageData record. Until it
// lands (parallel work), this parser returns plain (Missed, Covered) pairs so
// it builds standalone; ticket 10/integration will unify the value type.
internal static class CoberturaCoverageParser
{
    private static readonly IReadOnlyDictionary<string, (int Missed, int Covered)> _emptyMap =
        new Dictionary<string, (int Missed, int Covered)>();

    public static IReadOnlyDictionary<string, (int Missed, int Covered)> Parse(string? coberturaXmlPath)
    {
        if (string.IsNullOrEmpty(coberturaXmlPath) || !File.Exists(coberturaXmlPath))
        {
            return _emptyMap;
        }

        try
        {
            var settings = NewSecureReaderSettings();
            using var reader = XmlReader.Create(coberturaXmlPath, settings);
            var coverage = new Dictionary<string, (int Missed, int Covered)>(StringComparer.Ordinal);
            var currentClass = string.Empty;
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }
                if (reader.Name == "class")
                {
                    currentClass = NormalizeClassName(reader.GetAttribute("name"));
                }
                else if (reader.Name == "method")
                {
                    ReadMethodCoverage(reader, currentClass, coverage);
                }
            }

            if (coverage.Count == 0)
            {
                return _emptyMap;
            }
            return coverage;
        }
        catch (Exception ex) when (ex is XmlException || ex is IOException)
        {
            throw new InvalidOperationException("Unable to parse Cobertura XML: " + coberturaXmlPath, ex);
        }
    }

    public static XmlReaderSettings NewSecureReaderSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
    };

    private static void ReadMethodCoverage(
        XmlReader reader, string currentClass, Dictionary<string, (int Missed, int Covered)> coverage)
    {
        var methodName = reader.GetAttribute("name");
        if (string.IsNullOrEmpty(methodName) || string.IsNullOrEmpty(currentClass))
        {
            return;
        }
        if (reader.IsEmptyElement)
        {
            return;
        }

        var missed = 0;
        var covered = 0;
        var minLine = int.MaxValue;
        var hasLine = false;

        // <method><lines><line number=".." hits=".."/>...</lines></method>
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "method")
            {
                break;
            }
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "line")
            {
                continue;
            }
            hasLine = true;
            var hits = ParseInt(reader.GetAttribute("hits"));
            if (hits > 0)
            {
                covered++;
            }
            else
            {
                missed++;
            }
            var number = ParseInt(reader.GetAttribute("number"));
            if (number < minLine)
            {
                minLine = number;
            }
        }

        if (!hasLine)
        {
            return;
        }

        coverage[currentClass.CoverageKey(methodName, minLine)] = (missed, covered);
    }

    private static string NormalizeClassName(string? className)
    {
        if (string.IsNullOrEmpty(className))
        {
            return string.Empty;
        }
        return string.Create(className.Length, className, static (span, name) =>
        {
            name.AsSpan().CopyTo(span);
            for (var i = 0; i < span.Length; i++)
            {
                if (span[i] == '/' || span[i] == '\\')
                {
                    span[i] = '.';
                }
            }
        });
    }

    private static int ParseInt(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }
        return 0;
    }
}