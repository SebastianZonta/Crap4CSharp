using System.Diagnostics;

namespace Crap4CSharp;

internal sealed class ProcessCommandExecutor : ICommandExecutor
{
    public int Run(IReadOnlyList<string> args, string workingDirectory) =>
        RunAsync(args, workingDirectory, null).GetAwaiter().GetResult();

    public Task<int> RunAsync(
        IReadOnlyList<string> args, string workingDirectory, CancellationToken cancellationToken = default) =>
        RunAsync(args, workingDirectory, null, cancellationToken);

    public async Task<int> RunAsync(
        IReadOnlyList<string> args, string workingDirectory, TextWriter? output,
        CancellationToken cancellationToken = default)
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

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start process: " + args[0]);
        try
        {
            // Event-based reads: line-oriented like the child console output; avoids
            // passing the process StreamReaders into tasks (crashes the CA2025 analyzer).
            var sink = output is null ? null : TextWriter.Synchronized(output);
            var stdoutDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var stderrDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    stdoutDone.TrySetResult();
                }
                else
                {
                    sink?.WriteLine(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    stderrDone.TrySetResult();
                }
                else
                {
                    sink?.WriteLine(e.Data);
                }
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                await Task.WhenAll(stdoutDone.Task, stderrDone.Task).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                process.TryKillTree();
                throw;
            }
            return process.ExitCode;
        }
        finally
        {
            process.Dispose();
        }
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