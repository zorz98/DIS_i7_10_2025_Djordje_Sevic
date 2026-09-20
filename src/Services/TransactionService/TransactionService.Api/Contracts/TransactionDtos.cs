using TransactionService.Domain;

namespace TransactionService.Api.Contracts;

public sealed record CreateTransactionRequest(int CompanyId, string Category, decimal Amount, string Currency, DateOnly Date);

public sealed record TransactionDto(Guid Id, int CompanyId, string Category, decimal Amount, string Currency, DateOnly Date, string Status)
{
    public static TransactionDto FromDomain(Transaction transaction) => new(
        transaction.Id,
        transaction.CompanyId,
        transaction.Category,
        transaction.Amount,
        transaction.Currency,
        transaction.Date,
        transaction.Status.ToString());
}
