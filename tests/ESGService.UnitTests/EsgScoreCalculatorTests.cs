using ESGService.Domain;
using FluentAssertions;
using Xunit;

namespace ESGService.UnitTests;

public class EsgScoreCalculatorTests
{
    private readonly EsgScoreCalculator _calculator = new();

    [Fact]
    public void Should_Give_High_Environmental_Score_For_Low_Emission_Category()
    {
        var score = _calculator.Calculate(co2Kg: 100m, emissionFactor: 0.10m);

        score.EnvironmentalScore.Should().Be(98);
    }

    [Fact]
    public void Should_Give_Lower_Environmental_Score_For_High_Emission_Category()
    {
        var score = _calculator.Calculate(co2Kg: 11550m, emissionFactor: 2.31m);

        score.EnvironmentalScore.Should().Be(54);
    }

    [Fact]
    public void OverallScore_Should_Be_Penalized_By_Large_Co2_Footprint()
    {
        var score = _calculator.Calculate(co2Kg: 11550m, emissionFactor: 2.31m);

        score.OverallScore.Should().BeLessThan(score.EnvironmentalScore);
    }

    [Fact]
    public void Scores_Should_Never_Go_Below_Zero()
    {
        var score = _calculator.Calculate(co2Kg: 1_000_000m, emissionFactor: 5m);

        score.EnvironmentalScore.Should().Be(0);
        score.OverallScore.Should().Be(0);
    }
}
