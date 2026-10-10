namespace Crap4CSharp.Tests;

public sealed class CoverageDataTest
{
    [Fact]
    public void ZeroInstructionsMeansZeroPercent()
    {
        Assert.Equal(0.0, new CoverageData(0, 0).CoveragePercent());
    }

    [Fact]
    public void ComputesCoveredShare()
    {
        Assert.Equal(75.0, new CoverageData(1, 3).CoveragePercent(), 0.001);
    }
}
