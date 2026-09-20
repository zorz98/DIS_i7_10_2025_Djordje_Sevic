using ESGService.Api.Contracts;
using ESGService.Domain;
using Microsoft.AspNetCore.Mvc;

namespace ESGService.Api.Controllers;

[ApiController]
[Route("esg")]
public sealed class EsgController(IEsgResultRepository repository) : ControllerBase
{
    [HttpGet("transaction/{transactionId:guid}")]
    public async Task<ActionResult<EsgResultDto>> GetByTransaction(Guid transactionId, CancellationToken cancellationToken)
    {
        var result = await repository.GetByTransactionIdAsync(transactionId, cancellationToken);
        return result is null ? NotFound() : Ok(EsgResultDto.FromDomain(result));
    }

    [HttpGet("company/{companyId:int}")]
    public async Task<ActionResult<IReadOnlyList<EsgResultDto>>> GetByCompany(int companyId, CancellationToken cancellationToken)
    {
        var results = await repository.GetByCompanyAsync(companyId, cancellationToken);
        return Ok(results.Select(EsgResultDto.FromDomain));
    }
}
