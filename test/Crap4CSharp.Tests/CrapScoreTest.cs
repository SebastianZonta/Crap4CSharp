namespace Crap4CSharp.Tests;

public sealed class CrapScoreTest
{
    [Fact]
    public void ReturnsComplexityWhenFullyCovered()
    {
        Assert.Equal(5.0, CrapScore.Calculate(5, 100.0)!.Value, 0.0001);
    }

    [Fact]
    public void ReturnsCcSquaredPlusCcWhenUncovered()
    {
        Assert.Equal(30.0, CrapScore.Calculate(5, 0.0)!.Value, 0.0001);
    }

    [Fact]
    public void ComputesPartialCoverage()
    {
        Assert.Equal(18.648, CrapScore.Calculate(8, 45.0)!.Value, 0.01);
    }

    [Fact]
    public void ReturnsNullForUnknownCoverage()
    {
        Assert.Null(CrapScore.Calculate(3, null));
    }
}
