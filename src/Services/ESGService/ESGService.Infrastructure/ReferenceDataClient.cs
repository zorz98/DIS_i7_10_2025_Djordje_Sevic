using System.Net;
using System.Net.Http.Json;
using ESGService.Domain;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;

namespace ESGService.Infrastructure;

public sealed class ReferenceDataClient(HttpClient httpClient, ILogger<ReferenceDataClient> logger) : IReferenceDataClient
{
    private sealed record EmissionFactorDto(string Category, decimal Co2FactorPerEur);

    public async Task<ReferenceDataLookupResult> GetEmissionFactorAsync(string category, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"categories/{category}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new ReferenceDataLookupResult(true, null);
            }

            response.EnsureSuccessStatusCode();
            var dto = await response.Content.ReadFromJsonAsync<EmissionFactorDto>(cancellationToken);
            return new ReferenceDataLookupResult(true, dto?.Co2FactorPerEur);
        }
        catch (Exception ex) when (ex is BrokenCircuitException or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "ReferenceDataService is unavailable while resolving category {Category}", category);
            return new ReferenceDataLookupResult(false, null);
        }
    }
}
