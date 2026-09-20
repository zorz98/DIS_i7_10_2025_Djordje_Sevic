using NotificationService.Domain;

namespace NotificationService.Api.Contracts;

public sealed record NotificationDto(Guid Id, Guid TransactionId, int CompanyId, string Message, int OverallScore, DateTimeOffset SentAt)
{
    public static NotificationDto FromDomain(Notification notification) => new(
        notification.Id,
        notification.TransactionId,
        notification.CompanyId,
        notification.Message,
        notification.OverallScore,
        notification.SentAt);
}
