namespace Crap4CSharp.Tests;

public class NoProgressTest
{
    [Fact]
    public void NoProgressFlagDefaultsFalseAndParses()
    {
        Assert.False(CliArgumentsParser.Parse(Array.Empty<string>()).NoProgress);
        Assert.True(CliArgumentsParser.Parse(["--no-progress"]).NoProgress);
    }

    [Fact]
    public void NoProgressEqualsFormThrows()
    {
        Assert.Throws<InvalidOperationException>(
            () => CliArgumentsParser.Parse(["--no-progress=x"]));
    }
}