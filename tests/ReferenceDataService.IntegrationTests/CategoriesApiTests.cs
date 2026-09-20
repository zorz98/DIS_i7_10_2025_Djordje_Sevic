using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ReferenceDataService.IntegrationTests;

public class CategoriesApiTests(ReferenceDataServiceApiFactory factory) : IClassFixture<ReferenceDataServiceApiFactory>
{
    private sealed record EmissionFactorDto(string Category, decimal Co2FactorPerEur);

    [Fact]
    public async Task GetAll_Should_Return_Seeded_Categories_From_Real_Database()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/categories");
        response.EnsureSuccessStatusCode();

        var categories = await response.Content.ReadFromJsonAsync<List<EmissionFactorDto>>();

        categories.Should().NotBeNull();
        categories!.Should().Contain(c => c.Category == "Fuel" && c.Co2FactorPerEur == 2.31m);
        categories.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetByCategory_Should_Return_NotFound_For_Unknown_Category()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/categories/DoesNotExist");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByCategory_Should_Return_Consistent_Result_Across_Repeated_Calls_With_Redis_Cache()
    {
        var client = factory.CreateClient();

        var first = await client.GetFromJsonAsync<EmissionFactorDto>("/categories/Fuel");
        var second = await client.GetFromJsonAsync<EmissionFactorDto>("/categories/Fuel");

        first.Should().BeEquivalentTo(new EmissionFactorDto("Fuel", 2.31m));
        second.Should().BeEquivalentTo(first);
    }
}
