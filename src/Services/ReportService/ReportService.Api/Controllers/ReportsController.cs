using Microsoft.AspNetCore.Mvc;
using ReportService.Api.Contracts;
using ReportService.Domain;

namespace ReportService.Api.Controllers;

[ApiController]
[Route("reports")]
public sealed class ReportsController(ITransactionRecordRepository repository, ReportAggregator aggregator) : ControllerBase
{
    [HttpGet("company/{companyId:int}")]
    public async Task<ActionResult<CompanyReportDto>> GetCompanyReport(
        int companyId, [FromQuery] int month, [FromQuery] int year, CancellationToken cancellationToken)
    {
        var records = await repository.GetByCompanyAndPeriodAsync(companyId, year, month, cancellationToken);
        var report = aggregator.Aggregate(companyId, year, month, records);
        return Ok(CompanyReportDto.FromDomain(report));
    }
}
