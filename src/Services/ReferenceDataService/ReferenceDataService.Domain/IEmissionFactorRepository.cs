namespace ReferenceDataService.Domain;

public interface IEmissionFactorRepository
{
    Task<IReadOnlyList<EmissionFactor>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<EmissionFactor?> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
}
