namespace NotificationService.Domain;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken = default);
}
