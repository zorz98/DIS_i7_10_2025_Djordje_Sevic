using ReferenceDataService.Domain;

namespace ReferenceDataService.Api.Contracts;

public sealed record EmissionFactorDto(string Category, decimal Co2FactorPerEur)
{
    public static EmissionFactorDto FromDomain(EmissionFactor factor) =>
        new(factor.Category, factor.Co2FactorPerEur);
}
