using System.Diagnostics.Metrics;

namespace NotificationService.Domain;

public sealed class NotificationMetrics
{
    public const string MeterName = "GreenFinance.NotificationService";

    private readonly Counter<long> _sentCounter;

    public NotificationMetrics(Meter meter)
    {
        _sentCounter = meter.CreateCounter<long>("notifications.sent", description: "Number of low-ESG-score notifications sent");
    }

    public void RecordSent() => _sentCounter.Add(1);
}
