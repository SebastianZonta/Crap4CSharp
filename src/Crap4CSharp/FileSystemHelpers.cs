namespace Crap4CSharp;

// Single home for file-system concerns shared across modules: the
// enumeration-options factory (file discovery + coverage-file search),
// root-containment checks, and race-tolerant stale-coverage deletion.
// Span overloads keep callers allocation-free until materialization.
internal static class FileSystemHelpers
{
    internal static EnumerationOptions SharedEnumeration() => new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
    };

    internal static bool IsUnderDirectory(this ReadOnlySpan<char> path, ReadOnlySpan<char> directory, ReadOnlySpan<char> prefix) =>
        path.Equals(directory, StringComparison.Ordinal) ||
        path.StartsWith(prefix, StringComparison.Ordinal);

    internal static bool IsUnderDirectory(this string? path, string directory, string prefix) =>
        IsUnderDirectory(path.AsSpan(), directory.AsSpan(), prefix.AsSpan());

    internal static void DeleteIfExists(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            File.Delete(path);
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException || ex is FileNotFoundException)
        {
            // Lost a delete/create race with a concurrent run; nothing left to do.
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Failed deleting stale coverage: " + path, ex);
        }
    }
}