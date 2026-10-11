using System.Globalization;

namespace Crap4CSharp;

internal static class ToolConstants
{
    public const string SrcDirName = "src";
    public const string CSharpExtension = ".cs";
    public const string CSharpSearchPattern = "*.cs";

    public const string TestResultsDirName = "TestResults";
    public const string CoberturaFileName = "coverage.cobertura.xml";

    public const string CoverageKeyMethodSeparator = "#";
    public const char CoverageKeyLineSeparator = ':';

    public const double DefaultCrapThreshold = 8.0;

    public const string NoFilesMessage = "No C# files to analyze.";
    public const string ReportTitle = "CRAP Report\n===========\n";

    public static string CoverageMissingWarning(string moduleRoot) =>
        "Warning: Cobertura XML not found at "
        + Path.Combine(moduleRoot, TestResultsDirName) + ". Coverage will be N/A.";

    public static string ThresholdExceededMessage(double max, double threshold) => string.Format(
        CultureInfo.InvariantCulture, "CRAP threshold exceeded: {0:F1} > {1:F1}", max, threshold);

    public static string CombineRooted(string root, string path) =>
        Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(root, path));

    public static string CombineRooted(ReadOnlySpan<char> root, ReadOnlySpan<char> path) =>
        Path.IsPathRooted(path) ? Path.GetFullPath(new string(path)) : Path.GetFullPath(Path.Combine(new string(root), new string(path)));
}