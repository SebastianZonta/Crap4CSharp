namespace Crap4CSharp.Tests;

public class CliArgumentsParserTest
{
    [Fact]
    public void NoArgsMeansAllSrcFiles()
    {
        CliArguments args = CliArgumentsParser.Parse(Array.Empty<string>());
        Assert.Equal(CliMode.AllSrc, args.Mode);
    }

    [Fact]
    public void ChangedFlagMeansChangedSrcFiles()
    {
        CliArguments args = CliArgumentsParser.Parse(new[] { "--changed" });
        Assert.Equal(CliMode.ChangedSrc, args.Mode);
    }

    [Fact]
    public void FileNamesMeanExplicitFiles()
    {
        CliArguments args = CliArgumentsParser.Parse(new[] { "src/demo/A.cs", "src/demo/B.cs" });
        Assert.Equal(CliMode.ExplicitFiles, args.Mode);
        Assert.Equal(new[] { "src/demo/A.cs", "src/demo/B.cs" }, args.FileArgs);
    }

    [Fact]
    public void UnknownFlagsAreIgnoredWhenCollectingExplicitFiles()
    {
        CliArguments args = CliArgumentsParser.Parse(new[] { "src/demo/A.cs", "--bogus", "src/demo/B.cs" });

        Assert.Equal(CliMode.ExplicitFiles, args.Mode);
        Assert.Equal(new[] { "src/demo/A.cs", "src/demo/B.cs" }, args.FileArgs);
    }

    [Fact]
    public void HelpPrintsUsageMode()
    {
        CliArguments args = CliArgumentsParser.Parse(new[] { "--help" });
        Assert.Equal(CliMode.Help, args.Mode);
    }

    [Fact]
    public void ChangedCannotBeCombinedWithFiles()
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(new[] { "--changed", "src/demo/A.cs" }));
    }

    [Fact]
    public void PlainFilesDoNotTriggerChangedMode()
    {
        CliArguments args = CliArgumentsParser.Parse(new[] { "src/demo/A.cs" });

        Assert.Equal(CliMode.ExplicitFiles, args.Mode);
        Assert.Equal(new[] { "src/demo/A.cs" }, args.FileArgs);
    }
}
