using System.Diagnostics;

namespace Crap4CSharp;

internal sealed class ProcessCommandExecutor : ICommandExecutor
{
    public int Run(IReadOnlyList<string> args, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count == 0)
        {
            throw new ArgumentException("Command must not be empty.", nameof(args));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = args[0],
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };
        // ArgumentList passes each element as a single argv entry (execvp),
        // so args containing spaces or semicolons need no shell quoting.
        foreach (string arg in args.Skip(1))
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start process: " + args[0]);
        process.WaitForExit();
        return process.ExitCode;
    }
}
