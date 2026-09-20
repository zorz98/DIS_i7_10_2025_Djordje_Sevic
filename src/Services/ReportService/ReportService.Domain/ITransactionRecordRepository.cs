namespace ReportService.Domain;

public interface ITransactionRecordRepository
{
    Task AddTransactionAsync(
        Guid transactionId, int companyId, string category, decimal amount, DateOnly date,
        CancellationToken cancellationToken = default);

    Task ApplyEsgResultAsync(
        Guid transactionId, decimal co2Kg, int overallScore, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionRecord>> GetByCompanyAndPeriodAsync(
        int companyId, int year, int month, CancellationToken cancellationToken = default);
}
