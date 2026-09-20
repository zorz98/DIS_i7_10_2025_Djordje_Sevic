namespace ESGService.Domain;

public sealed class EsgCalculationService(
    IReferenceDataClient referenceDataClient,
    Co2Calculator co2Calculator,
    EsgScoreCalculator scoreCalculator,
    IEsgResultRepository repository,
    EsgMetrics metrics)
{
    public async Task<EsgResult> ProcessAsync(
        Guid transactionId, int companyId, string category, decimal amount, CancellationToken cancellationToken = default)
    {
        var lookup = await referenceDataClient.GetEmissionFactorAsync(category, cancellationToken);

        var result = lookup.IsAvailable
            ? Calculate(transactionId, companyId, category, amount, lookup.Co2FactorPerEur ?? 0m)
            : EsgResult.TemporarilyUnavailable(transactionId, companyId, category);

        await repository.UpsertAsync(result, cancellationToken);

        if (result.Status == EsgResultStatus.Calculated)
        {
            metrics.RecordCalculated();
        }
        else
        {
            metrics.RecordUnavailable();
        }

        return result;
    }

    private EsgResult Calculate(Guid transactionId, int companyId, string category, decimal amount, decimal emissionFactor)
    {
        var co2Kg = co2Calculator.Calculate(amount, emissionFactor);
        var score = scoreCalculator.Calculate(co2Kg, emissionFactor);
        return EsgResult.Calculated(transactionId, companyId, category, co2Kg, score.EnvironmentalScore, score.OverallScore);
    }
}
