using System.Diagnostics;

namespace Crap4CSharp;

internal static class ChangedFileDetector
{
    public static IReadOnlyList<string> ChangedCsFiles(string projectRoot) =>
        ChangedCsFilesAsync(projectRoot).GetAwaiter().GetResult();

    public static async Task<IReadOnlyList<string>> ChangedCsFilesAsync(
        string projectRoot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectRoot);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(projectRoot))
        {
            throw new DirectoryNotFoundException("Project directory not found: " + projectRoot);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // Structured argv: paths with spaces or quotes need no shell quoting.
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(projectRoot);
        startInfo.ArgumentList.Add("status");
        startInfo.ArgumentList.Add("--porcelain");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("git status failed: could not start git process");
        // Concurrent async reads: sequential sync ReadToEnd can deadlock when
        // either pipe buffer fills on large status output.
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKillTree(process);
            throw;
        }
        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "git status failed with exit " + process.ExitCode + ": " + (stdout + stderr));
        }

        return ParseChangedFiles(projectRoot, stdout);
    }

    public static IReadOnlyList<string> ChangedCsFilesUnderSrc(string projectRoot) =>
        ChangedCsFilesUnderSrcAsync(projectRoot).GetAwaiter().GetResult();

    public static async Task<IReadOnlyList<string>> ChangedCsFilesUnderSrcAsync(
        string projectRoot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectRoot);
        string src = Path.GetFullPath(Path.Combine(projectRoot, "src"));
        string prefix = src + Path.DirectorySeparatorChar;
        List<string> files = [];
        foreach (string path in await ChangedCsFilesAsync(projectRoot, cancellationToken).ConfigureAwait(false))
        {
            if (IsUnderDirectory(path, src, prefix))
            {
                files.Add(path);
            }
        }
        return files.ToArray();
    }

    private static string[] ParseChangedFiles(string root, string stdout)
    {
        List<string> files = [];
        ReadOnlySpan<char> output = stdout.AsSpan();
        int start = 0;
        while (true)
        {
            int newline = output.Slice(start).IndexOf('\n');
            ReadOnlySpan<char> line = newline < 0
                ? output.Slice(start)
                : output.Slice(start, newline);
            if (line.Length > 0 && line[line.Length - 1] == '\r')
            {
                line = line.Slice(0, line.Length - 1);
            }
            string? file = ParseStatusLine(root, line);
            if (file is not null)
            {
                files.Add(file);
            }
            if (newline < 0)
            {
                break;
            }
            start += newline + 1;
        }
        files.Sort(StringComparer.Ordinal);
        return files.ToArray();
    }

    private static string? ParseStatusLine(string root, ReadOnlySpan<char> line)
    {
        if (!IsCandidateLine(line))
        {
            return null;
        }
        ReadOnlySpan<char> finalPath = RenameTarget(line.Slice(3).Trim());
        if (!IsCsPath(finalPath))
        {
            return null;
        }
        return Path.GetFullPath(Path.Combine(root, finalPath.ToString()));
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

    private static bool IsCandidateLine(ReadOnlySpan<char> line)
    {
        if (line.Length < 4)
        {
            return false;
        }
        foreach (char c in line)
        {
            if (!char.IsWhiteSpace(c))
            {
                return true;
            }
        }
        return false;
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

    private static ReadOnlySpan<char> RenameTarget(ReadOnlySpan<char> pathPart)
    {
        int index = pathPart.IndexOf(" -> ".AsSpan(), StringComparison.Ordinal);
        if (index < 0)
        {
            return pathPart;
        }
        return pathPart.Slice(index + 4);
    }

    private static bool IsCsPath(ReadOnlySpan<char> path) =>
        path.EndsWith(".cs".AsSpan(), StringComparison.OrdinalIgnoreCase);

    private static bool IsUnderDirectory(string path, string directory, string prefix)
    {
        return path.Equals(directory, StringComparison.Ordinal)
            || path.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static void TryKillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is NotSupportedException)
        {
        }
    }
}
