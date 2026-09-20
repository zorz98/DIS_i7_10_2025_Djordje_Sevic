using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using ReportService.Api.Contracts;
using ReportService.Domain;

namespace ReportService.Api.Controllers;

[ApiController]
[Route("reports")]
public sealed class ReportsController(
    ITransactionRecordRepository repository,
    ReportAggregator aggregator,
    IDistributedCache cache,
    ILogger<ReportsController> logger) : ControllerBase
{
    [HttpGet("company/{companyId:int}")]
    public async Task<ActionResult<CompanyReportDto>> GetCompanyReport(
        int companyId, [FromQuery] int month, [FromQuery] int year, CancellationToken cancellationToken)
    {
        var key = ReportCache.KeyFor(companyId, year, month);
        var cached = await ReportCache.TryGetAsync(cache, key, logger, cancellationToken);
        if (cached is not null)
        {
            return Ok(JsonSerializer.Deserialize<CompanyReportDto>(cached));
        }

        var records = await repository.GetByCompanyAndPeriodAsync(companyId, year, month, cancellationToken);
        var report = aggregator.Aggregate(companyId, year, month, records);
        var dto = CompanyReportDto.FromDomain(report);

        await ReportCache.TrySetAsync(cache, key, JsonSerializer.SerializeToUtf8Bytes(dto), logger, cancellationToken);
        return Ok(dto);
    }
}
