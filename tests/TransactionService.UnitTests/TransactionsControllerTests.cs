using FluentAssertions;
using GreenFinance.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TransactionService.Api.Contracts;
using TransactionService.Api.Controllers;
using TransactionService.Domain;
using Xunit;

namespace TransactionService.UnitTests;

public class TransactionsControllerTests
{
    private readonly Mock<ITransactionRepository> _repository = new();
    private readonly Mock<IPublishEndpoint> _publishEndpoint = new();

    private TransactionsController CreateController() => new(_repository.Object, _publishEndpoint.Object);

    [Fact]
    public async Task Create_Should_Persist_Transaction_And_Publish_TransactionCreatedEvent()
    {
        var controller = CreateController();
        var request = new CreateTransactionRequest(12, "Fuel", 5000m, "EUR", new DateOnly(2026, 9, 20));

        var result = await controller.Create(request, CancellationToken.None);

        _repository.Verify(r => r.AddAsync(It.Is<Transaction>(t =>
            t.CompanyId == request.CompanyId &&
            t.Category == request.Category &&
            t.Amount == request.Amount), It.IsAny<CancellationToken>()), Times.Once);

        _publishEndpoint.Verify(p => p.Publish(
            It.Is<TransactionCreatedEvent>(e => e.CompanyId == request.CompanyId && e.Category == request.Category),
            It.IsAny<CancellationToken>()), Times.Once);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task GetById_Should_Return_NotFound_When_Transaction_Does_Not_Exist()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        var controller = CreateController();

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }
}
