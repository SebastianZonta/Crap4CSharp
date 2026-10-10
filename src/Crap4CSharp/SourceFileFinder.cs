namespace Crap4CSharp;

internal static class SourceFileFinder
{
    public static IReadOnlyList<string> FindAllCsFilesUnderSrc(string projectRoot)
    {
        string src = Path.Combine(projectRoot, "src");
        if (!Directory.Exists(src))
        {
            return [];
        }

        return Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }
}
