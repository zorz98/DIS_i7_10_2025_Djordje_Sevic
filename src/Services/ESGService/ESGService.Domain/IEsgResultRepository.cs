namespace ESGService.Domain;

public interface IEsgResultRepository
{
    Task UpsertAsync(EsgResult result, CancellationToken cancellationToken = default);

    Task<EsgResult?> GetByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EsgResult>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken = default);
}
