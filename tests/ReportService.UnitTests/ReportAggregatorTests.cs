using FluentAssertions;
using ReportService.Domain;
using Xunit;

namespace ReportService.UnitTests;

public class ReportAggregatorTests
{
    private readonly ReportAggregator _aggregator = new();

    [Fact]
    public void Aggregate_Should_Sum_Expenses_And_Co2_And_Average_Score()
    {
        var records = new List<TransactionRecord>
        {
            new() { TransactionId = Guid.NewGuid(), CompanyId = 123, Category = "Fuel", Amount = 5000m, Date = new DateOnly(2026, 9, 5), Co2Kg = 11550m, OverallScore = 31 },
            new() { TransactionId = Guid.NewGuid(), CompanyId = 123, Category = "Electricity", Amount = 2000m, Date = new DateOnly(2026, 9, 10), Co2Kg = 900m, OverallScore = 91 },
        };

        var report = _aggregator.Aggregate(123, 2026, 9, records);

        report.TotalExpenses.Should().Be(7000m);
        report.TotalCo2.Should().Be(12450m);
        report.EsgScore.Should().Be(61d);
        report.TransactionCount.Should().Be(2);
    }

    [Fact]
    public void Aggregate_Should_Ignore_Records_Without_Esg_Score_When_Averaging()
    {
        var records = new List<TransactionRecord>
        {
            new() { TransactionId = Guid.NewGuid(), CompanyId = 123, Category = "Fuel", Amount = 1000m, Date = new DateOnly(2026, 9, 1), Co2Kg = null, OverallScore = null },
            new() { TransactionId = Guid.NewGuid(), CompanyId = 123, Category = "Electricity", Amount = 500m, Date = new DateOnly(2026, 9, 2), Co2Kg = 225m, OverallScore = 80 },
        };

        var report = _aggregator.Aggregate(123, 2026, 9, records);

        report.TotalExpenses.Should().Be(1500m);
        report.TotalCo2.Should().Be(225m);
        report.EsgScore.Should().Be(80d);
    }

    [Fact]
    public void Aggregate_Should_Return_Null_EsgScore_When_No_Transaction_Has_Been_Scored_Yet()
    {
        var records = new List<TransactionRecord>
        {
            new() { TransactionId = Guid.NewGuid(), CompanyId = 123, Category = "Fuel", Amount = 1000m, Date = new DateOnly(2026, 9, 1) },
        };

        var report = _aggregator.Aggregate(123, 2026, 9, records);

        report.EsgScore.Should().BeNull();
    }

    [Fact]
    public void Aggregate_Should_Return_Zeroes_For_Empty_Period()
    {
        var report = _aggregator.Aggregate(123, 2026, 9, []);

        report.TotalExpenses.Should().Be(0m);
        report.TotalCo2.Should().Be(0m);
        report.TransactionCount.Should().Be(0);
        report.EsgScore.Should().BeNull();
    }
}
