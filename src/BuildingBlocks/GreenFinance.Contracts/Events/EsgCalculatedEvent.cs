namespace GreenFinance.Contracts.Events;

/// <summary>
/// Published by ESGService after computing the ESG result for a transaction.
/// Consumed by ReportService and NotificationService.
/// </summary>
public sealed record EsgCalculatedEvent(
    Guid TransactionId,
    int CompanyId,
    string Category,
    decimal Co2Kg,
    int EnvironmentalScore,
    int OverallScore,
    DateTimeOffset CalculatedAt);
