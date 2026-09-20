using System.Diagnostics.Metrics;
using ESGService.Domain;
using FluentAssertions;
using Moq;
using Xunit;

namespace ESGService.UnitTests;

public class EsgCalculationServiceTests
{
    private readonly Mock<IReferenceDataClient> _referenceDataClient = new();
    private readonly Mock<IEsgResultRepository> _repository = new();
    private readonly Co2Calculator _co2Calculator = new();
    private readonly EsgScoreCalculator _scoreCalculator = new();
    private readonly EsgMetrics _metrics = new(new Meter("ESGService.UnitTests"));

    private EsgCalculationService CreateService() =>
        new(_referenceDataClient.Object, _co2Calculator, _scoreCalculator, _repository.Object, _metrics);

    [Fact]
    public async Task ProcessAsync_Should_Save_Calculated_Result_When_ReferenceDataService_Is_Available()
    {
        _referenceDataClient
            .Setup(c => c.GetEmissionFactorAsync("Fuel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReferenceDataLookupResult(true, 2.31m));

        var service = CreateService();
        var transactionId = Guid.NewGuid();

        var result = await service.ProcessAsync(transactionId, 12, "Fuel", 5000m, CancellationToken.None);

        result.Status.Should().Be(EsgResultStatus.Calculated);
        result.Co2Kg.Should().Be(11550m);

        _repository.Verify(r => r.UpsertAsync(
            It.Is<EsgResult>(e => e.Status == EsgResultStatus.Calculated && e.TransactionId == transactionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_Should_Save_TemporarilyUnavailable_When_ReferenceDataService_Is_Down()
    {
        _referenceDataClient
            .Setup(c => c.GetEmissionFactorAsync("Fuel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReferenceDataLookupResult(false, null));

        var service = CreateService();
        var transactionId = Guid.NewGuid();

        var result = await service.ProcessAsync(transactionId, 12, "Fuel", 5000m, CancellationToken.None);

        result.Status.Should().Be(EsgResultStatus.TemporarilyUnavailable);

        _repository.Verify(r => r.UpsertAsync(
            It.Is<EsgResult>(e => e.Status == EsgResultStatus.TemporarilyUnavailable),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
