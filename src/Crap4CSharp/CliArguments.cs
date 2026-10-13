namespace Crap4CSharp;

internal sealed record CliArguments(
    CliMode Mode,
    IReadOnlyList<string> FileArgs,
    string? ProjectPath = null,
    string? OutputPath = null,
    double Threshold = 8.0,
    string Format = "text",
    int? Top = null,
    int MinComplexity = 1,
    bool NoCoverage = false,
    bool NoProgress = false,
    bool ViolationsOnly = false,
    string? DiffRef = null);