using Microsoft.EntityFrameworkCore;
using ReportService.Domain;

namespace ReportService.Infrastructure;

public sealed class TransactionRecordRepository(ReportDbContext dbContext) : ITransactionRecordRepository
{
    public async Task AddTransactionAsync(
        Guid transactionId, int companyId, string category, decimal amount, DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.TransactionRecords.FindAsync([transactionId], cancellationToken);
        if (existing is not null)
        {
            return;
        }

        dbContext.TransactionRecords.Add(new TransactionRecord
        {
            TransactionId = transactionId,
            CompanyId = companyId,
            Category = category,
            Amount = amount,
            Date = date,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyEsgResultAsync(
        Guid transactionId, decimal co2Kg, int overallScore, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.TransactionRecords.FindAsync([transactionId], cancellationToken);
        if (record is null)
        {
            // TransactionCreated is expected to be processed before EsgCalculated; if it was not,
            // there is nothing to attach the ESG result to yet.
            return;
        }

        record.Co2Kg = co2Kg;
        record.OverallScore = overallScore;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionRecord>> GetByCompanyAndPeriodAsync(
        int companyId, int year, int month, CancellationToken cancellationToken = default) =>
        await dbContext.TransactionRecords.AsNoTracking()
            .Where(r => r.CompanyId == companyId && r.Date.Year == year && r.Date.Month == month)
            .ToListAsync(cancellationToken);
}
