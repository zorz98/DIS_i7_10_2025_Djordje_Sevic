using System.Diagnostics.Metrics;

namespace TransactionService.Domain;

public sealed class TransactionMetrics
{
    public const string MeterName = "GreenFinance.TransactionService";

    private readonly Counter<long> _createdCounter;

    public TransactionMetrics(Meter meter)
    {
        _createdCounter = meter.CreateCounter<long>("transactions.created", description: "Number of transactions created");
    }

    public void RecordCreated() => _createdCounter.Add(1);
}
