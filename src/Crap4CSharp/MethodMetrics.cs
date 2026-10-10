namespace Crap4CSharp;

internal sealed record MethodMetrics(
    string MethodName,
    string ClassName,
    int Complexity,
    double? CoveragePercent,
    double? CrapScore);
