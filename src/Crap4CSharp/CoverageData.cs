namespace Crap4CSharp;

internal readonly record struct CoverageData(int MissedInstructions, int CoveredInstructions)
{
    public double CoveragePercent()
    {
        int total = MissedInstructions + CoveredInstructions;
        if (total == 0)
        {
            return 0.0;
        }

        return (CoveredInstructions * 100.0) / total;
    }
}

internal static class CoverageKeyExtensions
{
    public static string CoverageKey(this string className, string methodName, int line) =>
        string.Create(
            null,
            $"{className}{ToolConstants.CoverageKeyMethodSeparator}{methodName}{ToolConstants.CoverageKeyLineSeparator}{line}");

    public static string MethodPrefix(this string className, string methodName) =>
        string.Create(
            null,
            $"{className}{ToolConstants.CoverageKeyMethodSeparator}{methodName}{ToolConstants.CoverageKeyLineSeparator}");

    public static string MethodKey(this string className, string methodName) =>
        string.Create(
            null,
            $"{className}{ToolConstants.CoverageKeyMethodSeparator}{methodName}");
}
