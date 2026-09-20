namespace TransactionService.Domain;

public sealed class Transaction
{
    public Guid Id { get; set; }

    public int CompanyId { get; set; }

    public required string Category { get; set; }

    public decimal Amount { get; set; }

    public required string Currency { get; set; }

    public DateOnly Date { get; set; }

    public TransactionStatus Status { get; set; }

    public static Transaction Create(int companyId, string category, decimal amount, string currency, DateOnly date) =>
        new()
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Category = category,
            Amount = amount,
            Currency = currency,
            Date = date,
            Status = TransactionStatus.Created,
        };
}
