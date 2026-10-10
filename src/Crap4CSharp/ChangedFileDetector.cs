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
            process.TryKillTree();
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
        string src = Path.GetFullPath(Path.Combine(projectRoot, ToolConstants.SrcDirName));
        string prefix = src + Path.DirectorySeparatorChar;
        List<string> files = [];
        foreach (string path in await ChangedCsFilesAsync(projectRoot, cancellationToken).ConfigureAwait(false))
        {
            if (path.IsUnderDirectory(src, prefix))
            {
                files.Add(path);
            }
        }
        return files.ToArray();
    }

    private static string[] ParseChangedFiles(ReadOnlySpan<char> root, ReadOnlySpan<char> stdout)
    {
        List<string> files = [];
        ReadOnlySpan<char> output = stdout;
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

    private static string? ParseStatusLine(ReadOnlySpan<char> root, ReadOnlySpan<char> line)
    {
        if (!line.IsCandidateLine())
        {
            return null;
        }
        ReadOnlySpan<char> finalPath = line.Slice(3).Trim().RenameTarget();
        if (!IsCsPath(finalPath))
        {
            return null;
        }
        return ToolConstants.CombineRooted(root, finalPath);
    }

    public static bool IsCandidateLine(this string? line) =>
        line is not null && IsCandidateLine(line.AsSpan());

    private static bool IsCandidateLine(this ReadOnlySpan<char> line)
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

    public static string RenameTarget(this string pathPart) =>
        RenameTarget(pathPart.AsSpan()).ToString();

    private static ReadOnlySpan<char> RenameTarget(this ReadOnlySpan<char> pathPart)
    {
        int index = pathPart.IndexOf(" -> ", StringComparison.Ordinal);
        if (index < 0)
        {
            return pathPart;
        }
        return pathPart.Slice(index + 4);
    }

    private static bool IsCsPath(ReadOnlySpan<char> path) =>
        path.EndsWith(ToolConstants.CSharpExtension, StringComparison.OrdinalIgnoreCase);
}
