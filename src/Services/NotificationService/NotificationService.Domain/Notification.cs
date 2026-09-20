namespace NotificationService.Domain;

public sealed class Notification
{
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }

    public int CompanyId { get; set; }

    public required string Message { get; set; }

    public int OverallScore { get; set; }

    public DateTimeOffset SentAt { get; set; }

    public const int LowScoreThreshold = 40;

    public static Notification ForLowEsgScore(Guid transactionId, int companyId, int overallScore) => new()
    {
        Id = Guid.NewGuid(),
        TransactionId = transactionId,
        CompanyId = companyId,
        OverallScore = overallScore,
        Message = "Your ESG score requires attention",
        SentAt = DateTimeOffset.UtcNow,
    };

    public static bool ShouldNotify(int overallScore) => overallScore < LowScoreThreshold;
}
