using Microsoft.AspNetCore.Mvc;
using ReferenceDataService.Api.Contracts;
using ReferenceDataService.Domain;

namespace ReferenceDataService.Api.Controllers;

[ApiController]
[Route("emission-factors")]
public sealed class EmissionFactorsController(IEmissionFactorRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmissionFactorDto>>> GetAll(CancellationToken cancellationToken)
    {
        var factors = await repository.GetAllAsync(cancellationToken);
        return Ok(factors.Select(EmissionFactorDto.FromDomain));
    }
}
