using Microsoft.EntityFrameworkCore;
using ReferenceDataService.Domain;

namespace ReferenceDataService.Infrastructure;

public sealed class EmissionFactorRepository(ReferenceDataDbContext dbContext) : IEmissionFactorRepository
{
    public async Task<IReadOnlyList<EmissionFactor>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.EmissionFactors.AsNoTracking().OrderBy(e => e.Category).ToListAsync(cancellationToken);

    public async Task<EmissionFactor?> GetByCategoryAsync(string category, CancellationToken cancellationToken = default) =>
        await dbContext.EmissionFactors.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Category == category, cancellationToken);
}
