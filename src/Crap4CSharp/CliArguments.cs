namespace Crap4CSharp;

internal sealed record CliArguments(CliMode Mode, IReadOnlyList<string> FileArgs);
