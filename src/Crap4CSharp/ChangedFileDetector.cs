using System.Diagnostics;

namespace Crap4CSharp;

internal static class ChangedFileDetector
{
    public static IReadOnlyList<string> ChangedCsFiles(string projectRoot)
    {
        var startInfo = new ProcessStartInfo("git", $"-C \"{projectRoot}\" status --porcelain")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("git status failed: could not start git process");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("git status failed: " + (stdout + stderr));
        }

        List<string> files = [];
        foreach (string line in stdout.Split('\n'))
        {
            string trimmedEnd = line.TrimEnd('\r');
            string? file = ParseStatusLine(projectRoot, trimmedEnd);
            if (file is not null)
            {
                files.Add(file);
            }
        }
        files.Sort(StringComparer.Ordinal);
        return files.ToArray();
    }

    public static IReadOnlyList<string> ChangedCsFilesUnderSrc(string projectRoot)
    {
        string src = Path.GetFullPath(Path.Combine(projectRoot, "src"));
        string prefix = src + Path.DirectorySeparatorChar;
        List<string> files = [];
        foreach (string path in ChangedCsFiles(projectRoot))
        {
            if (IsUnderDirectory(path, src, prefix))
            {
                files.Add(path);
            }
        }
        return files.ToArray();
    }

    private static string? ParseStatusLine(string root, string line)
    {
        if (!IsCandidateLine(line))
        {
            return null;
        }
        string pathPart = line.Substring(3).Trim();
        string finalPath = RenameTarget(pathPart);
        if (!IsCsPath(finalPath))
        {
            return null;
        }
        return Path.GetFullPath(Path.Combine(root, finalPath));
    }

    public static bool IsCandidateLine(string? line)
    {
        if (line is null)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }
        return line.Length >= 4;
    }

    public static string RenameTarget(string pathPart)
    {
        int index = pathPart.IndexOf(" -> ", StringComparison.Ordinal);
        if (index < 0)
        {
            return pathPart;
        }
        return pathPart.Substring(index + 4);
    }

    private static bool IsCsPath(string path)
    {
        return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderDirectory(string path, string directory, string prefix)
    {
        return path.Equals(directory, StringComparison.Ordinal)
            || path.StartsWith(prefix, StringComparison.Ordinal);
    }
}
