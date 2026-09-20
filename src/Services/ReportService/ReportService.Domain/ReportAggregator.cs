namespace ReportService.Domain;

public sealed class ReportAggregator
{
    public CompanyReport Aggregate(int companyId, int year, int month, IReadOnlyList<TransactionRecord> records)
    {
        var totalExpenses = records.Sum(r => r.Amount);
        var totalCo2 = records.Sum(r => r.Co2Kg ?? 0m);

        var scored = records.Where(r => r.OverallScore.HasValue).ToList();
        double? esgScore = scored.Count > 0 ? scored.Average(r => r.OverallScore!.Value) : null;

        return new CompanyReport(companyId, year, month, totalExpenses, totalCo2, esgScore, records.Count);
    }
}
