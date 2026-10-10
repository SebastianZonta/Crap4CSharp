using System.Xml;

namespace Crap4CSharp;

// Mirrors crap4java.JacocoCoverageParser (final class + private ctor + statics)
// adapted to Cobertura XML (coverlet): per <method>, covered = lines with
// hits > 0, missed = lines with hits == 0, key "class#method:minLine".
//
// Coverage value: ticket 07 owns the top-level CoverageData record. Until it
// lands (parallel work), this parser returns plain (Missed, Covered) pairs so
// it builds standalone; ticket 10/integration will unify the value type.
public static class CoberturaCoverageParser
{
    public static IReadOnlyDictionary<string, (int Missed, int Covered)> Parse(string? coberturaXmlPath)
    {
        if (string.IsNullOrEmpty(coberturaXmlPath) || !File.Exists(coberturaXmlPath))
        {
            return new Dictionary<string, (int Missed, int Covered)>();
        }

        try
        {
            var settings = NewSecureReaderSettings();
            using var reader = XmlReader.Create(coberturaXmlPath, settings);
            var document = new XmlDocument { XmlResolver = null };
            document.Load(reader);

            var coverage = new Dictionary<string, (int Missed, int Covered)>(StringComparer.Ordinal);
            var methods = document.GetElementsByTagName("method");
            foreach (XmlNode methodNode in methods)
            {
                if (methodNode is not XmlElement method)
                {
                    continue;
                }
                string? key;
                (int Missed, int Covered) counts;
                if (TryReadMethod(method, out key, out counts) && key is not null)
                {
                    coverage[key] = counts;
                }
            }

            return coverage;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Unable to parse Cobertura XML: " + coberturaXmlPath, ex);
        }
    }

    public static XmlReaderSettings NewSecureReaderSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
    };

    private static bool TryReadMethod(XmlElement method, out string? key, out (int Missed, int Covered) counts)
    {
        key = null;
        counts = (0, 0);

        string methodName = method.GetAttribute("name");
        if (string.IsNullOrEmpty(methodName))
        {
            return false;
        }

        string className = FindClassName(method);
        if (string.IsNullOrEmpty(className))
        {
            return false;
        }
        className = className.Replace('/', '.').Replace('\\', '.');

        int missed = 0;
        int covered = 0;
        int minLine = int.MaxValue;
        bool hasLine = false;

        // <method><lines><line number=".." hits=".."/>...</lines></method>
        foreach (XmlNode child in method.GetElementsByTagName("line"))
        {
            if (child is not XmlElement line || !IsDescendantOf(line, method))
            {
                continue;
            }
            hasLine = true;
            int hits = ParseInt(line.GetAttribute("hits"));
            if (hits > 0)
            {
                covered++;
            }
            else
            {
                missed++;
            }
            int number = ParseInt(line.GetAttribute("number"));
            if (number < minLine)
            {
                minLine = number;
            }
        }

        if (!hasLine)
        {
            return false;
        }

        key = className + "#" + methodName + ":" + minLine;
        counts = (missed, covered);
        return true;
    }

    private static string FindClassName(XmlNode node)
    {
        for (XmlNode? current = node.ParentNode; current is not null; current = current.ParentNode)
        {
            if (current is XmlElement element && element.Name == "class")
            {
                return element.GetAttribute("name");
            }
        }
        return string.Empty;
    }

    private static bool IsDescendantOf(XmlElement candidate, XmlElement ancestor)
    {
        for (XmlNode? current = candidate.ParentNode; current is not null; current = current.ParentNode)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }
        return false;
    }

    private static int ParseInt(string value)
    {
        try
        {
            return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentException)
        {
            return 0;
        }
    }
}
