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

        return FindAllCsFilesUnderTree(src);
    }

    public static IReadOnlyList<string> FindAllCsFilesForDirectory(string directory)
    {
        var underSrc = FindAllCsFilesUnderSrc(directory);
        return underSrc.Count != 0 ? underSrc : FindAllCsFilesUnderTree(directory);
    }

    public static IReadOnlyList<string> FindAllCsFilesUnderTree(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        var options = FileSystemHelpers.SharedEnumeration();
        options.MatchCasing = MatchCasing.CaseInsensitive;
        string[] files =
        [
            .. Directory.EnumerateFiles(root, ToolConstants.CSharpSearchPattern, options)
                .Where(f => !HasBuildOutputSegment(Path.GetFullPath(f))),
        ];
        Array.Sort(files, StringComparer.Ordinal);
        return files;
    }

    internal static bool IsSolutionFile(string path) =>
        path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);

    internal static bool IsCsprojFile(string path) =>
        path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

    internal static bool HasBuildOutputSegment(string path)
    {
        foreach (var segment in path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (string.Equals(segment, "bin", StringComparison.Ordinal)
                || string.Equals(segment, "obj", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}