namespace GreenFinance.Contracts.Events;

/// <summary>
/// Published by TransactionService after a transaction has been persisted.
/// Consumed by ESGService and ReportService.
/// </summary>
public sealed record TransactionCreatedEvent(
    Guid TransactionId,
    int CompanyId,
    string Category,
    decimal Amount,
    string Currency,
    DateOnly Date);
