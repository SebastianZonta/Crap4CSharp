namespace Crap4CSharp;

internal static class SourceFileFinder
{
    private static EnumerationOptions NewEnumeration() => new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MatchCasing = MatchCasing.CaseInsensitive,
    };

    public static IReadOnlyList<string> FindAllCsFilesUnderSrc(string projectRoot)
    {
        string src = Path.Combine(projectRoot, "src");
        if (!Directory.Exists(src))
        {
            return [];
        }

        string[] files = [.. Directory.EnumerateFiles(src, "*.cs", NewEnumeration())];
        Array.Sort(files, StringComparer.Ordinal);
        return files;
    }
}
