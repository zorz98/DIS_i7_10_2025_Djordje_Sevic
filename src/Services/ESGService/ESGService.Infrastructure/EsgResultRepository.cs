using Microsoft.EntityFrameworkCore;
using ESGService.Domain;

namespace ESGService.Infrastructure;

public sealed class EsgResultRepository(EsgDbContext dbContext) : IEsgResultRepository
{
    public async Task UpsertAsync(EsgResult result, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.EsgResults.FindAsync([result.TransactionId], cancellationToken);
        if (existing is null)
        {
            dbContext.EsgResults.Add(result);
        }
        else
        {
            dbContext.Entry(existing).CurrentValues.SetValues(result);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<EsgResult?> GetByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
        await dbContext.EsgResults.AsNoTracking().FirstOrDefaultAsync(r => r.TransactionId == transactionId, cancellationToken);

    public async Task<IReadOnlyList<EsgResult>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken = default) =>
        await dbContext.EsgResults.AsNoTracking()
            .Where(r => r.CompanyId == companyId)
            .OrderByDescending(r => r.CalculatedAt)
            .ToListAsync(cancellationToken);
}
