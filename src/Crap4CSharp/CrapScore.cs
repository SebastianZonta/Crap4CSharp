namespace Crap4CSharp;

internal static class CrapScore
{
    public static double? Calculate(int complexity, double? coveragePercent)
    {
        if (coveragePercent is null)
        {
            return null;
        }

        double cc = complexity;
        double uncovered = 1.0 - (coveragePercent.Value / 100.0);
        return (cc * cc * uncovered * uncovered * uncovered) + cc;
    }
}
