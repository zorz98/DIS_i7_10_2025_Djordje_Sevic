namespace ESGService.Domain;

public sealed class EsgResult
{
    public Guid TransactionId { get; set; }

    public int CompanyId { get; set; }

    public required string Category { get; set; }

    public EsgResultStatus Status { get; set; }

    public decimal Co2Kg { get; set; }

    public int EnvironmentalScore { get; set; }

    public int OverallScore { get; set; }

    public DateTimeOffset CalculatedAt { get; set; }

    public static EsgResult Calculated(
        Guid transactionId, int companyId, string category, decimal co2Kg, int environmentalScore, int overallScore) =>
        new()
        {
            TransactionId = transactionId,
            CompanyId = companyId,
            Category = category,
            Status = EsgResultStatus.Calculated,
            Co2Kg = co2Kg,
            EnvironmentalScore = environmentalScore,
            OverallScore = overallScore,
            CalculatedAt = DateTimeOffset.UtcNow,
        };

    public static EsgResult TemporarilyUnavailable(Guid transactionId, int companyId, string category) =>
        new()
        {
            TransactionId = transactionId,
            CompanyId = companyId,
            Category = category,
            Status = EsgResultStatus.TemporarilyUnavailable,
            CalculatedAt = DateTimeOffset.UtcNow,
        };
}
