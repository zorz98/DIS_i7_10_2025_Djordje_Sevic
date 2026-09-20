using ReportService.Domain;

namespace ReportService.Api.Contracts;

public sealed record CompanyReportDto(
    int CompanyId, int Year, int Month, decimal TotalExpenses, decimal TotalCo2, double? EsgScore, int Transactions)
{
    public static CompanyReportDto FromDomain(CompanyReport report) => new(
        report.CompanyId, report.Year, report.Month, report.TotalExpenses, report.TotalCo2, report.EsgScore, report.TransactionCount);
}
