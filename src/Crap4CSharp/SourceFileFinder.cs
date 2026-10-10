namespace Crap4CSharp;

public static class SourceFileFinder
{
    public static IReadOnlyList<string> FindAllCsFilesUnderSrc(string projectRoot)
    {
        string src = Path.Combine(projectRoot, "src");
        if (!Directory.Exists(src))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }
}
