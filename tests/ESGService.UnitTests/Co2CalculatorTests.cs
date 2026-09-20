using ESGService.Domain;
using FluentAssertions;
using Xunit;

namespace ESGService.UnitTests;

public class Co2CalculatorTests
{
    private readonly Co2Calculator _calculator = new();

    [Fact]
    public void Should_Calculate_Co2_For_Fuel_Transaction()
    {
        var result = _calculator.Calculate(amount: 1000, emissionFactor: 2.31m);

        result.Should().Be(2310);
    }

    [Fact]
    public void Should_Calculate_Co2_For_Larger_Fuel_Transaction()
    {
        var result = _calculator.Calculate(amount: 5000, emissionFactor: 2.31m);

        result.Should().Be(11550);
    }
}
