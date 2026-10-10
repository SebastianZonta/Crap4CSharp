namespace Crap4CSharp.Tests;

public class CliArgumentsParserTest
{
    private static readonly string[] ChangedFlag = ["--changed"];
    private static readonly string[] TwoFiles = ["src/demo/A.cs", "src/demo/B.cs"];
    private static readonly string[] HelpFlag = ["--help"];
    private static readonly string[] ChangedPlusFile = ["--changed", "src/demo/A.cs"];
    private static readonly string[] SingleFile = ["src/demo/A.cs"];

    [Fact]
    public void NoArgsMeansAllSrcFiles()
    {
        CliArguments args = CliArgumentsParser.Parse(Array.Empty<string>());
        Assert.Equal(CliMode.AllSrc, args.Mode);
    }

    [Fact]
    public void DefaultsAreBackwardCompatible()
    {
        CliArguments args = CliArgumentsParser.Parse(Array.Empty<string>());
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
        CliArguments args = CliArgumentsParser.Parse(ChangedFlag);
        Assert.Equal(CliMode.ChangedSrc, args.Mode);
    }

    [Fact]
    public void FileNamesMeanExplicitFiles()
    {
        CliArguments args = CliArgumentsParser.Parse(TwoFiles);
        Assert.Equal(CliMode.ExplicitFiles, args.Mode);
        Assert.Equal(TwoFiles, args.FileArgs);
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
        CliArguments args = CliArgumentsParser.Parse(HelpFlag);
        Assert.Equal(CliMode.Help, args.Mode);
    }

    [Fact]
    public void ShortHelpMeansHelpMode()
    {
        CliArguments args = CliArgumentsParser.Parse(["-h"]);
        Assert.Equal(CliMode.Help, args.Mode);
    }

    [Fact]
    public void QuestionMarkMeansHelpMode()
    {
        CliArguments args = CliArgumentsParser.Parse(["-?"]);
        Assert.Equal(CliMode.Help, args.Mode);
    }

    [Fact]
    public void VersionFlagMeansVersionMode()
    {
        CliArguments args = CliArgumentsParser.Parse(["--version"]);
        Assert.Equal(CliMode.Version, args.Mode);
    }

    [Fact]
    public void ChangedCannotBeCombinedWithFiles()
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(ChangedPlusFile));
    }

    [Fact]
    public void PlainFilesDoNotTriggerChangedMode()
    {
        CliArguments args = CliArgumentsParser.Parse(SingleFile);

        Assert.Equal(CliMode.ExplicitFiles, args.Mode);
        Assert.Equal(SingleFile, args.FileArgs);
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
        CliArguments args = CliArgumentsParser.Parse(["--path", "some/dir"]);
        Assert.Equal("some/dir", args.ProjectPath);
        Assert.Equal(CliMode.AllSrc, args.Mode);
    }

    [Fact]
    public void PathFlagEqualsForm()
    {
        CliArguments args = CliArgumentsParser.Parse(["--path=some/dir"]);
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
        CliArguments args = CliArgumentsParser.Parse(["--top", "5", "--min-cc", "2"]);
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
        CliArguments args = CliArgumentsParser.Parse(
            ["--threshold=5", "--format=json", "src/demo/A.cs"]);
        Assert.Equal(CliMode.ExplicitFiles, args.Mode);
        Assert.Equal(5.0, args.Threshold);
        Assert.Equal("json", args.Format);
        Assert.Equal(SingleFile, args.FileArgs);
    }

    [Fact]
    public void FlagsCombineWithChangedMode()
    {
        CliArguments args = CliArgumentsParser.Parse(["--changed", "--threshold=5", "--no-coverage"]);
        Assert.Equal(CliMode.ChangedSrc, args.Mode);
        Assert.Equal(5.0, args.Threshold);
        Assert.True(args.NoCoverage);
    }

    [Fact]
    public void DoubleDashSentinelTreatsRestAsFiles()
    {
        CliArguments args = CliArgumentsParser.Parse(["--", "--help", "--bogus"]);
        Assert.Equal(CliMode.ExplicitFiles, args.Mode);
        Assert.Equal(["--help", "--bogus"], args.FileArgs);
    }

    [Fact]
    public void DoubleDashSentinelAfterFlags()
    {
        CliArguments args = CliArgumentsParser.Parse(["--threshold=5", "--", "--top"]);
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
        CliArguments args = CliArgumentsParser.Parse(["/?"]);
        Assert.Equal(CliMode.Help, args.Mode);
    }

    [Theory]
    [InlineData("--help", "--version")]
    [InlineData("--version", "--help")]
    [InlineData("-?", "--version")]
    public void HelpWinsOverVersion(string first, string second)
    {
        CliArguments args = CliArgumentsParser.Parse([first, second]);
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
        CliArguments args = CliArgumentsParser.Parse(
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
    public void DoubleDashTreatsFlagsAsFilenames(string flag)
    {
        CliArguments args = CliArgumentsParser.Parse(["--", flag]);
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
