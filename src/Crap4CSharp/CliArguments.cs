namespace Crap4CSharp;

public sealed record CliArguments(CliMode Mode, IReadOnlyList<string> FileArgs);
