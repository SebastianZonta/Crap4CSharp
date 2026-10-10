namespace Crap4CSharp;

internal interface ICommandExecutor
{
    int Run(IReadOnlyList<string> args, string workingDirectory);
}
