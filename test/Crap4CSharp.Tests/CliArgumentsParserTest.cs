namespace Crap4CSharp.Tests;

public class CliArgumentsParserTest
{
    private static readonly string[] ChangedFlag = ["--changed"];
    private static readonly string[] TwoFiles = ["src/demo/A.cs", "src/demo/B.cs"];
    private static readonly string[] FilesWithBogusFlag = ["src/demo/A.cs", "--bogus", "src/demo/B.cs"];
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
    public void UnknownFlagsAreIgnoredWhenCollectingExplicitFiles()
    {
        CliArguments args = CliArgumentsParser.Parse(FilesWithBogusFlag);

        Assert.Equal(CliMode.ExplicitFiles, args.Mode);
        Assert.Equal(TwoFiles, args.FileArgs);
    }

    [Fact]
    public void HelpPrintsUsageMode()
    {
        CliArguments args = CliArgumentsParser.Parse(HelpFlag);
        Assert.Equal(CliMode.Help, args.Mode);
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
}
