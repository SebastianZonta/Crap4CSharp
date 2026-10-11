namespace Crap4CSharp.Tests;

public class ProgressSpinnerTest
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

    [Fact]
    public void SpinnerDisabledForNonConsoleWriterOrOptOut()
    {
        using var first = new StringWriter();
        using var second = new StringWriter();
        Assert.False(ProgressSpinner.IsEnabled(first, false));
        Assert.False(ProgressSpinner.IsEnabled(second, true));
    }

    [Fact]
    public async Task SpinnerWritesFrameWhenEnabled()
    {
        using var writer = new StringWriter();
        await using (ProgressSpinner.StartIfEnabled(
            writer, "Analyzing…", true, TimeSpan.Zero, TimeSpan.FromMilliseconds(10)))
        {
            await Task.Delay(100);
        }
        Assert.Contains("Analyzing", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpinnerWritesNothingWhenDisabled()
    {
        using var writer = new StringWriter();
        await using (ProgressSpinner.StartIfEnabled(
            writer, "Analyzing…", false, TimeSpan.Zero, TimeSpan.FromMilliseconds(10)))
        {
            await Task.Delay(50);
        }
        Assert.Equal(string.Empty, writer.ToString());
    }
}