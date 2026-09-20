using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ReferenceDataService.Api.Contracts;
using ReferenceDataService.Api.Controllers;
using ReferenceDataService.Domain;
using Xunit;

namespace ReferenceDataService.UnitTests;

public class CategoriesControllerTests
{
    private readonly Mock<IEmissionFactorRepository> _repository = new();

    [Fact]
    public async Task GetByCategory_Should_Return_NotFound_When_Category_Does_Not_Exist()
    {
        _repository
            .Setup(r => r.GetByCategoryAsync("Unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmissionFactor?)null);

        var controller = new CategoriesController(_repository.Object);

        var result = await controller.GetByCategory("Unknown", CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetByCategory_Should_Return_Factor_When_Category_Exists()
    {
        _repository
            .Setup(r => r.GetByCategoryAsync("Fuel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmissionFactor { Id = 1, Category = "Fuel", Co2FactorPerEur = 2.31m });

        var controller = new CategoriesController(_repository.Object);

        var result = await controller.GetByCategory("Fuel", CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new EmissionFactorDto("Fuel", 2.31m));
    }

    [Fact]
    public async Task GetAll_Should_Return_All_Factors_Mapped_To_Dto()
    {
        _repository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmissionFactor>
            {
                new() { Id = 1, Category = "Fuel", Co2FactorPerEur = 2.31m },
                new() { Id = 2, Category = "Electricity", Co2FactorPerEur = 0.45m },
            });

        var controller = new CategoriesController(_repository.Object);

        var result = await controller.GetAll(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new[]
        {
            new EmissionFactorDto("Fuel", 2.31m),
            new EmissionFactorDto("Electricity", 0.45m),
        });
    }
}
