namespace Crap4CSharp;

public sealed record MethodMetrics(
    string MethodName,
    string ClassName,
    int Complexity,
    double? CoveragePercent,
    double? CrapScore);
