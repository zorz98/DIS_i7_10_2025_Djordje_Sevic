namespace ESGService.Domain;

public sealed class Co2Calculator
{
    /// <summary>Kilograms of CO2 emitted for a given spend and category emission factor.</summary>
    public decimal Calculate(decimal amount, decimal emissionFactor) => amount * emissionFactor;
}
