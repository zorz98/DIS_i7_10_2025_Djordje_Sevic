using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace TransactionService.IntegrationTests;

public class TransactionsApiTests(TransactionServiceApiFactory factory) : IClassFixture<TransactionServiceApiFactory>
{
    private sealed record CreateTransactionRequest(int CompanyId, string Category, decimal Amount, string Currency, DateOnly Date);

    private sealed record TransactionDto(Guid Id, int CompanyId, string Category, decimal Amount, string Currency, DateOnly Date, string Status);

    [Fact]
    public async Task Should_Create_Transaction()
    {
        var client = factory.CreateClient();
        var request = new CreateTransactionRequest(12, "Fuel", 5000m, "EUR", new DateOnly(2026, 9, 20));

        var response = await client.PostAsJsonAsync("/transactions", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<TransactionDto>();
        created.Should().NotBeNull();
        created!.CompanyId.Should().Be(12);
        created.Category.Should().Be("Fuel");

        var getResponse = await client.GetAsync($"/transactions/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_Should_Return_NotFound_For_Unknown_Transaction()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/transactions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
