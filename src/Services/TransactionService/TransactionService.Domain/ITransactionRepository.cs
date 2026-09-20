namespace TransactionService.Domain;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);

    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken = default);
}
