using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GreenFinance.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ESGService.IntegrationTests;

public class EsgApiTests(ESGServiceApiFactory factory) : IClassFixture<ESGServiceApiFactory>
{
    private sealed record EsgResultDto(
        Guid TransactionId, int CompanyId, string Category, string Status,
        decimal? Co2Kg, int? EnvironmentalScore, int? OverallScore, DateTimeOffset CalculatedAt);

    [Fact]
    public async Task GetByTransaction_Should_Return_NotFound_For_Unknown_Transaction()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/esg/transaction/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Consumer_Should_Mark_Result_TemporarilyUnavailable_When_ReferenceDataService_Is_Unreachable()
    {
        var transactionId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
            await publishEndpoint.Publish(new TransactionCreatedEvent(
                transactionId, 12, "Fuel", 5000m, "EUR", new DateOnly(2026, 9, 20)));
        }

        var client = factory.CreateClient();
        EsgResultDto? result = null;

        for (var attempt = 0; attempt < 20 && result is null; attempt++)
        {
            await Task.Delay(500);
            var response = await client.GetAsync($"/esg/transaction/{transactionId}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                result = await response.Content.ReadFromJsonAsync<EsgResultDto>();
            }
        }

        result.Should().NotBeNull();
        result!.Status.Should().Be("temporarily_unavailable");
        result.Co2Kg.Should().BeNull();
    }
}
