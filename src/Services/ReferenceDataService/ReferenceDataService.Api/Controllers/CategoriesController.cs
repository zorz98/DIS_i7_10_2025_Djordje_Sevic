using Microsoft.AspNetCore.Mvc;
using ReferenceDataService.Api.Contracts;
using ReferenceDataService.Domain;

namespace ReferenceDataService.Api.Controllers;

[ApiController]
[Route("categories")]
public sealed class CategoriesController(IEmissionFactorRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmissionFactorDto>>> GetAll(CancellationToken cancellationToken)
    {
        var factors = await repository.GetAllAsync(cancellationToken);
        return Ok(factors.Select(EmissionFactorDto.FromDomain));
    }

    [HttpGet("{category}")]
    public async Task<ActionResult<EmissionFactorDto>> GetByCategory(string category, CancellationToken cancellationToken)
    {
        var factor = await repository.GetByCategoryAsync(category, cancellationToken);
        return factor is null ? NotFound() : Ok(EmissionFactorDto.FromDomain(factor));
    }
}
