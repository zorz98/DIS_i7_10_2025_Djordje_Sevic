namespace ReportService.Domain;

public sealed record CompanyReport(
    int CompanyId,
    int Year,
    int Month,
    decimal TotalExpenses,
    decimal TotalCo2,
    double? EsgScore,
    int TransactionCount);
