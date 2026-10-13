namespace Crap4CSharp.Tests;

public class CliArgumentsParserTest
{
    private static readonly string[] _changedFlag = ["--changed"];
    private static readonly string[] _twoFiles = ["src/demo/A.cs", "src/demo/B.cs"];
    private static readonly string[] _helpFlag = ["--help"];
    private static readonly string[] _changedPlusFile = ["--changed", "src/demo/A.cs"];
    private static readonly string[] _singleFile = ["src/demo/A.cs"];

    [Fact]
    public void NoArgsMeansAllSrcFiles()
    {
        var args = CliArgumentsParser.Parse(Array.Empty<string>());
        Assert.Equal(CliMode.AllSrc, args.Mode);
    }

    [Fact]
    public void DefaultsAreBackwardCompatible()
    {
        var args = CliArgumentsParser.Parse(Array.Empty<string>());
        Assert.Null(args.ProjectPath);
        Assert.Null(args.OutputPath);
        Assert.Equal(8.0, args.Threshold);
        Assert.Equal("text", args.Format);
        Assert.Null(args.Top);
        Assert.Equal(1, args.MinComplexity);
        Assert.False(args.NoCoverage);
        Assert.Empty(args.FileArgs);
    }

    [Fact]
    public void ChangedFlagMeansChangedSrcFiles()
    {
        var args = CliArgumentsParser.Parse(_changedFlag);
        Assert.Equal(CliMode.ChangedSrc, args.Mode);
    }

    [Fact]
    public void DiffFlagMeansDiffRefMode()
    {
        var args = CliArgumentsParser.Parse(["--diff", "origin/develop"]);
        Assert.Equal(CliMode.DiffRef, args.Mode);
        Assert.Equal("origin/develop", args.DiffRef);
    }

    [Fact]
    public void DiffFlagEqualsForm()
    {
        var args = CliArgumentsParser.Parse(["--diff=origin/develop"]);
        Assert.Equal(CliMode.DiffRef, args.Mode);
        Assert.Equal("origin/develop", args.DiffRef);
    }

    [Fact]
    public void DiffCannotBeCombinedWithFiles()
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--diff", "origin/develop", "src/demo/A.cs"]));
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["src/demo/A.cs", "--diff", "origin/develop"]));
    }

    [Fact]
    public void DiffCannotBeCombinedWithChanged()
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--changed", "--diff", "origin/develop"]));
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--diff", "origin/develop", "--changed"]));
    }

    [Fact]
    public void DiffMissingValueThrows()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--diff"]));
        Assert.Contains("--diff", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DiffEmptyValueThrows()
    {
        var blank = Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--diff="]));
        Assert.Contains("--diff", blank.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--diff", ""]));
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--diff", "   "]));
    }

    [Fact]
    public void FlagsCombineWithDiffMode()
    {
        var args = CliArgumentsParser.Parse(
            ["--diff", "origin/develop", "--threshold=5", "--no-coverage",
             "--format=json", "--top=3", "--min-cc=2", "--no-progress",
             "--violations-only", "--path=some/dir", "--output=r.txt"]);
        Assert.Equal(CliMode.DiffRef, args.Mode);
        Assert.Equal("origin/develop", args.DiffRef);
        Assert.Equal(5.0, args.Threshold);
        Assert.True(args.NoCoverage);
        Assert.Equal("json", args.Format);
        Assert.Equal(3, args.Top);
        Assert.Equal(2, args.MinComplexity);
        Assert.True(args.NoProgress);
        Assert.True(args.ViolationsOnly);
        Assert.Equal("some/dir", args.ProjectPath);
        Assert.Equal("r.txt", args.OutputPath);
    }

    [Fact]
    public void FileNamesMeanExplicitFiles()
    {
        var args = CliArgumentsParser.Parse(_twoFiles);
        Assert.Equal(CliMode.ExplicitFiles, args.Mode);
        Assert.Equal(_twoFiles, args.FileArgs);
    }

    [Fact]
    public void UnknownLongFlagThrows()
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["src/demo/A.cs", "--bogus", "src/demo/B.cs"]));
    }

    [Fact]
    public void HelpPrintsUsageMode()
    {
        var args = CliArgumentsParser.Parse(_helpFlag);
        Assert.Equal(CliMode.Help, args.Mode);
    }

    [Fact]
    public void ShortHelpMeansHelpMode()
    {
        var args = CliArgumentsParser.Parse(["-h"]);
        Assert.Equal(CliMode.Help, args.Mode);
    }

    [Fact]
    public void QuestionMarkMeansHelpMode()
    {
        var args = CliArgumentsParser.Parse(["-?"]);
        Assert.Equal(CliMode.Help, args.Mode);
    }

    [Fact]
    public void VersionFlagMeansVersionMode()
    {
        var args = CliArgumentsParser.Parse(["--version"]);
        Assert.Equal(CliMode.Version, args.Mode);
    }

    [Fact]
    public void ChangedCannotBeCombinedWithFiles()
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(_changedPlusFile));
    }

    [Fact]
    public void PlainFilesDoNotTriggerChangedMode()
    {
        var args = CliArgumentsParser.Parse(_singleFile);

        Assert.Equal(CliMode.ExplicitFiles, args.Mode);
        Assert.Equal(_singleFile, args.FileArgs);
    }

    [Fact]
    public void UnknownFlagAloneThrows()
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--bogus"]));
    }

    [Fact]
    public void UnknownShortFlagThrows()
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["-z"]));
    }

    [Fact]
    public void PathFlagSpaceForm()
    {
        var args = CliArgumentsParser.Parse(["--path", "some/dir"]);
        Assert.Equal("some/dir", args.ProjectPath);
        Assert.Equal(CliMode.AllSrc, args.Mode);
    }

    [Fact]
    public void PathFlagEqualsForm()
    {
        var args = CliArgumentsParser.Parse(["--path=some/dir"]);
        Assert.Equal("some/dir", args.ProjectPath);
    }

    [Fact]
    public void OutputFlagLongAndShortForms()
    {
        Assert.Equal("a.txt", CliArgumentsParser.Parse(["--output", "a.txt"]).OutputPath);
        Assert.Equal("b.txt", CliArgumentsParser.Parse(["--output=b.txt"]).OutputPath);
        Assert.Equal("c.txt", CliArgumentsParser.Parse(["-o", "c.txt"]).OutputPath);
        Assert.Equal("d.txt", CliArgumentsParser.Parse(["-o=d.txt"]).OutputPath);
    }

    [Fact]
    public void ThresholdFlagLongAndShortForms()
    {
        Assert.Equal(12.5, CliArgumentsParser.Parse(["--threshold", "12.5"]).Threshold);
        Assert.Equal(12.5, CliArgumentsParser.Parse(["--threshold=12.5"]).Threshold);
        Assert.Equal(3.0, CliArgumentsParser.Parse(["-t", "3.0"]).Threshold);
        Assert.Equal(3.0, CliArgumentsParser.Parse(["-t=3.0"]).Threshold);
    }

    [Fact]
    public void FormatFlagNormalizesCase()
    {
        Assert.Equal("json", CliArgumentsParser.Parse(["--format", "JSON"]).Format);
        Assert.Equal("csv", CliArgumentsParser.Parse(["-f=csv"]).Format);
        Assert.Equal("text", CliArgumentsParser.Parse(["--format=text"]).Format);
    }

    [Fact]
    public void TopAndMinCcFlags()
    {
        var args = CliArgumentsParser.Parse(["--top", "5", "--min-cc", "2"]);
        Assert.Equal(5, args.Top);
        Assert.Equal(2, args.MinComplexity);
        Assert.Equal(5, CliArgumentsParser.Parse(["--top=5"]).Top);
        Assert.Equal(2, CliArgumentsParser.Parse(["--min-cc=2"]).MinComplexity);
    }

    [Fact]
    public void NoCoverageFlag()
    {
        Assert.True(CliArgumentsParser.Parse(["--no-coverage"]).NoCoverage);
        Assert.False(CliArgumentsParser.Parse(Array.Empty<string>()).NoCoverage);
    }

    [Fact]
    public void FlagsCombineWithExplicitFiles()
    {
        var args = CliArgumentsParser.Parse(
            ["--threshold=5", "--format=json", "src/demo/A.cs"]);
        Assert.Equal(CliMode.ExplicitFiles, args.Mode);
        Assert.Equal(5.0, args.Threshold);
        Assert.Equal("json", args.Format);
        Assert.Equal(_singleFile, args.FileArgs);
    }

    [Fact]
    public void FlagsCombineWithChangedMode()
    {
        var args = CliArgumentsParser.Parse(["--changed", "--threshold=5", "--no-coverage"]);
        Assert.Equal(CliMode.ChangedSrc, args.Mode);
        Assert.Equal(5.0, args.Threshold);
        Assert.True(args.NoCoverage);
    }

    [Fact]
    public void DoubleDashSentinelTreatsRestAsFiles()
    {
        var args = CliArgumentsParser.Parse(["--", "--help", "--bogus"]);
        Assert.Equal(CliMode.ExplicitFiles, args.Mode);
        Assert.Equal(["--help", "--bogus"], args.FileArgs);
    }

    [Fact]
    public void DoubleDashSentinelAfterFlags()
    {
        var args = CliArgumentsParser.Parse(["--threshold=5", "--", "--top"]);
        Assert.Equal(CliMode.ExplicitFiles, args.Mode);
        Assert.Equal(["--top"], args.FileArgs);
        Assert.Equal(5.0, args.Threshold);
    }

    [Theory]
    [InlineData("--path")]
    [InlineData("--output")]
    [InlineData("-o")]
    [InlineData("--threshold")]
    [InlineData("-t")]
    [InlineData("--format")]
    [InlineData("-f")]
    [InlineData("--top")]
    [InlineData("--min-cc")]
    public void MissingValueThrows(string flag)
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse([flag]));
    }

    [Fact]
    public void EmptyEqualsValueThrows()
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--threshold="]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("NaN")]
    public void InvalidThresholdThrows(string raw)
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--threshold", raw]));
    }

    [Fact]
    public void InvalidFormatThrows()
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--format", "xml"]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("abc")]
    public void InvalidTopThrows(string raw)
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--top", raw]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("abc")]
    public void InvalidMinCcThrows(string raw)
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--min-cc", raw]));
    }

    [Fact]
    public void FlagThatTakesNoValueRejectsEqualsForm()
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--help=x"]));
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--changed=x"]));
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--version=x"]));
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--no-coverage=x"]));
    }

    [Fact]
    public void SlashQuestionMarkMeansHelpMode()
    {
        var args = CliArgumentsParser.Parse(["/?"]);
        Assert.Equal(CliMode.Help, args.Mode);
    }

    [Theory]
    [InlineData("--help", "--version")]
    [InlineData("--version", "--help")]
    [InlineData("-?", "--version")]
    public void HelpWinsOverVersion(string first, string second)
    {
        var args = CliArgumentsParser.Parse([first, second]);
        Assert.Equal(CliMode.Help, args.Mode);
    }

    [Theory]
    [InlineData("-h=x")]
    [InlineData("-?=x")]
    [InlineData("-h=")]
    public void ShortHelpEqualsFormRejected(string flag)
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse([flag]));
    }

    [Fact]
    public void LastValueWinsForRepeatedOptions()
    {
        var args = CliArgumentsParser.Parse(
            ["--threshold=1", "--threshold", "9", "--format=text", "--format=json",
             "--path=a", "--path=b", "--top=1", "--top=3"]);
        Assert.Equal(9.0, args.Threshold);
        Assert.Equal("json", args.Format);
        Assert.Equal("b", args.ProjectPath);
        Assert.Equal(3, args.Top);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--changed")]
    [InlineData("--diff")]
    public void DoubleDashTreatsFlagsAsFilenames(string flag)
    {
        var args = CliArgumentsParser.Parse(["--", flag]);
        Assert.Equal(CliMode.ExplicitFiles, args.Mode);
        Assert.Equal([flag], args.FileArgs);
    }

    [Theory]
    [InlineData("+Infinity")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void InfinityThresholdThrows(string raw)
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--threshold", raw]));
    }

    [Fact]
    public void YamlFormatThrows()
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--format", "yaml"]));
    }

    [Theory]
    [InlineData("--top=")]
    [InlineData("--min-cc=")]
    public void EmptyTopEqualsThrows(string flag)
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse([flag]));
    }

    [Fact]
    public void UnknownOptionEqualsFormThrows()
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--bogus=x"]));
    }
}