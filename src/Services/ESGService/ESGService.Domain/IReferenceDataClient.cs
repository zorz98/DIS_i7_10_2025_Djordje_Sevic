namespace ESGService.Domain;

public sealed record ReferenceDataLookupResult(bool IsAvailable, decimal? Co2FactorPerEur);

public interface IReferenceDataClient
{
    Task<ReferenceDataLookupResult> GetEmissionFactorAsync(string category, CancellationToken cancellationToken = default);
}
