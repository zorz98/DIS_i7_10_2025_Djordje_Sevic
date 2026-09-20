namespace ReportService.Domain;

public interface ITransactionRecordRepository
{
    /// <summary>Returns the affected record's period (year/month), used by callers to invalidate a per-period cache.</summary>
    Task<DateOnly> AddTransactionAsync(
        Guid transactionId, int companyId, string category, decimal amount, DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the affected record's period (year/month), used by callers to invalidate a per-period cache.</summary>
    Task<DateOnly> ApplyEsgResultAsync(
        Guid transactionId, int companyId, string category, decimal co2Kg, int overallScore,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionRecord>> GetByCompanyAndPeriodAsync(
        int companyId, int year, int month, CancellationToken cancellationToken = default);
}
