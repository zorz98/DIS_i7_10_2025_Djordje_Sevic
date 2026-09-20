using ESGService.Domain;

namespace ESGService.Api.Contracts;

public sealed record EsgResultDto(
    Guid TransactionId,
    int CompanyId,
    string Category,
    string Status,
    decimal? Co2Kg,
    int? EnvironmentalScore,
    int? OverallScore,
    DateTimeOffset CalculatedAt)
{
    public static EsgResultDto FromDomain(EsgResult result) =>
        result.Status == EsgResultStatus.TemporarilyUnavailable
            ? new EsgResultDto(result.TransactionId, result.CompanyId, result.Category, "temporarily_unavailable", null, null, null, result.CalculatedAt)
            : new EsgResultDto(result.TransactionId, result.CompanyId, result.Category, "calculated", result.Co2Kg, result.EnvironmentalScore, result.OverallScore, result.CalculatedAt);
}
