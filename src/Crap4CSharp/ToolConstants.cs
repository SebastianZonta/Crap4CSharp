using System.Globalization;

namespace Crap4CSharp;

// Single home for the literals shared across the tool: layout names,
// coverage file locations, the coverage-key format, defaults, and
// user-facing messages. Message values are asserted by tests — keep them stable.
internal static class ToolConstants
{
    public const string SrcDirName = "src";
    public const string CSharpExtension = ".cs";
    public const string CSharpSearchPattern = "*.cs";

    public const string TestResultsDirName = "TestResults";
    public const string CoberturaFileName = "coverage.cobertura.xml";

    // Coverage key format "class#method:line", built by the parser, read by the analyzer.
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

    // Span entry point: callers holding spans materialize once here, at the
    // framework boundary, instead of once per check upstream.
    public static string CombineRooted(ReadOnlySpan<char> root, ReadOnlySpan<char> path) =>
        Path.IsPathRooted(path) ? Path.GetFullPath(new string(path)) : Path.GetFullPath(Path.Combine(new string(root), new string(path)));
}