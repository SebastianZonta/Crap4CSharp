using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Crap4CSharp.Tests")]

namespace Crap4CSharp;

internal interface ICommandExecutor
{
    int Run(IReadOnlyList<string> args, string workingDirectory);
}
