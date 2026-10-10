namespace Crap4CSharp;

public sealed record CoverageData(int MissedInstructions, int CoveredInstructions)
{
    public double CoveragePercent()
    {
        int total = MissedInstructions + CoveredInstructions;
        if (total == 0)
        {
            return 0.0;
        }

        return (CoveredInstructions * 100.0) / total;
    }
}
