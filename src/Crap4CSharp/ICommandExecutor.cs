namespace Crap4CSharp;

internal interface ICommandExecutor
{
    int Run(IReadOnlyList<string> args, string workingDirectory);

    Task<int> RunAsync(
        IReadOnlyList<string> args, string workingDirectory, CancellationToken cancellationToken = default) =>
        Task.FromResult(Run(args, workingDirectory));
}
