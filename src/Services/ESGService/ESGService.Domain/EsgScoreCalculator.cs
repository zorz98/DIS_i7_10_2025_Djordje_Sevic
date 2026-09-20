namespace ESGService.Domain;

public sealed record EsgScore(int EnvironmentalScore, int OverallScore);

public sealed class EsgScoreCalculator
{
    /// <summary>
    /// environmentalScore reflects how clean the spending category is (lower emission factor -> higher score).
    /// overallScore additionally penalizes the absolute CO2 footprint of the transaction.
    /// </summary>
    public EsgScore Calculate(decimal co2Kg, decimal emissionFactor)
    {
        var environmentalScore = Math.Clamp(100 - (int)(emissionFactor * 20m), 0, 100);
        var overallScore = Math.Clamp(environmentalScore - (int)(co2Kg / 500m), 0, 100);
        return new EsgScore(environmentalScore, overallScore);
    }
}
