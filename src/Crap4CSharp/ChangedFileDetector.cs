using System.Diagnostics;

namespace Crap4CSharp;

public static class ChangedFileDetector
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

        var files = new List<string>();
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
        return files.AsReadOnly();
    }

    public static IReadOnlyList<string> ChangedCsFilesUnderSrc(string projectRoot)
    {
        string src = Path.GetFullPath(Path.Combine(projectRoot, "src"));
        return ChangedCsFiles(projectRoot)
            .Where(path => IsUnderDirectory(Path.GetFullPath(path), src))
            .ToList()
            .AsReadOnly();
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
        return path.EndsWith(".cs", StringComparison.Ordinal);
    }

    private static bool IsUnderDirectory(string path, string directory)
    {
        return path.Equals(directory, StringComparison.Ordinal)
            || path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
