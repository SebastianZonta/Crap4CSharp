using System.Diagnostics;

namespace Crap4CSharp;

internal sealed class ProcessCommandExecutor : ICommandExecutor
{
    public int Run(IReadOnlyList<string> args, string workingDirectory) =>
        RunAsync(args, workingDirectory).GetAwaiter().GetResult();

    public async Task<int> RunAsync(
        IReadOnlyList<string> args, string workingDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(workingDirectory);
        if (args.Count == 0)
        {
            throw new ArgumentException("Command must not be empty.", nameof(args));
        }
        if (!Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException("Working directory not found: " + workingDirectory);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = args[0],
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args.Skip(1))
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start process: " + args[0]);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.TryKillTree();
            throw;
        }
        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        return process.ExitCode;
    }
}

internal static class ProcessExtensions
{
    internal static void TryKillTree(this Process process)
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