using Microsoft.EntityFrameworkCore;
using TransactionService.Domain;

namespace TransactionService.Infrastructure;

public sealed class TransactionRepository(TransactionDbContext dbContext) : ITransactionRepository
{
    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Transactions.AsNoTracking().OrderByDescending(t => t.Date).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken = default) =>
        await dbContext.Transactions.AsNoTracking()
            .Where(t => t.CompanyId == companyId)
            .OrderByDescending(t => t.Date)
            .ToListAsync(cancellationToken);
}
