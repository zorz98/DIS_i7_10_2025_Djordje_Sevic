namespace NotificationService.Domain;

public sealed class NotificationDecisionService(INotificationRepository repository, NotificationMetrics metrics)
{
    /// <summary>Returns the created notification, or null when the ESG score does not warrant one.</summary>
    public async Task<Notification?> ProcessAsync(
        Guid transactionId, int companyId, int overallScore, CancellationToken cancellationToken = default)
    {
        if (!Notification.ShouldNotify(overallScore))
        {
            return null;
        }

        var notification = Notification.ForLowEsgScore(transactionId, companyId, overallScore);
        await repository.AddAsync(notification, cancellationToken);
        metrics.RecordSent();
        return notification;
    }
}
