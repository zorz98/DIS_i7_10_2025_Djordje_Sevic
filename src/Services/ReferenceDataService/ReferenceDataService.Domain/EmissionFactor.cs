namespace ReferenceDataService.Domain;

public sealed class EmissionFactor
{
    public int Id { get; set; }

    public required string Category { get; set; }

    /// <summary>Kilograms of CO2 emitted per unit currency (EUR) spent in this category.</summary>
    public decimal Co2FactorPerEur { get; set; }
}
