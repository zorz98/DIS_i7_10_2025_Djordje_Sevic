namespace ReportService.Domain;

/// <summary>
/// Local read-model row, built entirely from TransactionCreated and EsgCalculated events.
/// </summary>
public sealed class TransactionRecord
{
    public Guid TransactionId { get; set; }

    public int CompanyId { get; set; }

    public required string Category { get; set; }

    public decimal Amount { get; set; }

    public DateOnly Date { get; set; }

    public decimal? Co2Kg { get; set; }

    public int? OverallScore { get; set; }
}
