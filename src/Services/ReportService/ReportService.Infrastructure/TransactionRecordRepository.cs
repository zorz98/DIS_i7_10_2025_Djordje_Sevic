using Microsoft.EntityFrameworkCore;
using ReportService.Domain;

namespace ReportService.Infrastructure;

public sealed class TransactionRecordRepository(ReportDbContext dbContext) : ITransactionRecordRepository
{
    public Task<DateOnly> AddTransactionAsync(
        Guid transactionId, int companyId, string category, decimal amount, DateOnly date,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(
            transactionId,
            createIfMissing: () => new TransactionRecord
            {
                TransactionId = transactionId,
                CompanyId = companyId,
                Category = category,
                Amount = amount,
                Date = date,
            },
            // EsgCalculated may have arrived first (no cross-queue ordering guarantee) and
            // created a placeholder row; fill in the transaction details onto it.
            applyTo: record =>
            {
                record.CompanyId = companyId;
                record.Category = category;
                record.Amount = amount;
                record.Date = date;
            },
            cancellationToken);

    public Task<DateOnly> ApplyEsgResultAsync(
        Guid transactionId, int companyId, string category, decimal co2Kg, int overallScore,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(
            transactionId,
            // TransactionCreated may not have been processed yet; create a placeholder row so
            // the ESG result is not lost, to be filled in once TransactionCreated arrives.
            createIfMissing: () => new TransactionRecord
            {
                TransactionId = transactionId,
                CompanyId = companyId,
                Category = category,
                Amount = 0m,
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Co2Kg = co2Kg,
                OverallScore = overallScore,
            },
            applyTo: record =>
            {
                record.Co2Kg = co2Kg;
                record.OverallScore = overallScore;
            },
            cancellationToken);

    /// <summary>
    /// Upserts by TransactionId. The two consumers race on rows they both may create, so a
    /// concurrent insert can violate the primary key; when that happens we fall back to
    /// updating the row the other consumer just committed.
    /// </summary>
    private async Task<DateOnly> UpsertAsync(
        Guid transactionId,
        Func<TransactionRecord> createIfMissing,
        Action<TransactionRecord> applyTo,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.TransactionRecords.FindAsync([transactionId], cancellationToken);
        if (existing is not null)
        {
            applyTo(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
            return existing.Date;
        }

        var created = createIfMissing();
        dbContext.TransactionRecords.Add(created);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return created.Date;
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var record = await dbContext.TransactionRecords.SingleAsync(r => r.TransactionId == transactionId, cancellationToken);
            applyTo(record);
            await dbContext.SaveChangesAsync(cancellationToken);
            return record.Date;
        }
    }

    public async Task<IReadOnlyList<TransactionRecord>> GetByCompanyAndPeriodAsync(
        int companyId, int year, int month, CancellationToken cancellationToken = default) =>
        await dbContext.TransactionRecords.AsNoTracking()
            .Where(r => r.CompanyId == companyId && r.Date.Year == year && r.Date.Month == month)
            .ToListAsync(cancellationToken);
}
