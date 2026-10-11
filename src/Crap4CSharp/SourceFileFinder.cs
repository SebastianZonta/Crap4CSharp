namespace Crap4CSharp;

internal static class SourceFileFinder
{
    public static IReadOnlyList<string> FindAllCsFilesUnderSrc(string projectRoot)
    {
        var src = Path.Combine(projectRoot, ToolConstants.SrcDirName);
        if (!Directory.Exists(src))
        {
            return [];
        }

        var options = FileSystemHelpers.SharedEnumeration();
        options.MatchCasing = MatchCasing.CaseInsensitive;
        string[] files = [.. Directory.EnumerateFiles(src, ToolConstants.CSharpSearchPattern, options)];
        Array.Sort(files, StringComparer.Ordinal);
        return files;
    }
}