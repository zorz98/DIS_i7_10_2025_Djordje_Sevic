using FluentAssertions;
using TransactionService.Domain;
using Xunit;

namespace TransactionService.UnitTests;

public class TransactionTests
{
    [Fact]
    public void Create_Should_Initialize_Transaction_With_Created_Status()
    {
        var date = new DateOnly(2026, 9, 20);

        var transaction = Transaction.Create(12, "Fuel", 5000m, "EUR", date);

        transaction.Id.Should().NotBeEmpty();
        transaction.CompanyId.Should().Be(12);
        transaction.Category.Should().Be("Fuel");
        transaction.Amount.Should().Be(5000m);
        transaction.Currency.Should().Be("EUR");
        transaction.Date.Should().Be(date);
        transaction.Status.Should().Be(TransactionStatus.Created);
    }
}
