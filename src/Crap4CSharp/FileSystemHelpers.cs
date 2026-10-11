namespace Crap4CSharp;

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
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Failed deleting stale coverage: " + path, ex);
        }
    }
}