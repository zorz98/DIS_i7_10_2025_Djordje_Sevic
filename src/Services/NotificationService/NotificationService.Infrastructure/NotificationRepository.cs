using Microsoft.EntityFrameworkCore;
using NotificationService.Domain;

namespace NotificationService.Infrastructure;

public sealed class NotificationRepository(NotificationDbContext dbContext) : INotificationRepository
{
    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken = default) =>
        await dbContext.Notifications.AsNoTracking()
            .Where(n => n.CompanyId == companyId)
            .OrderByDescending(n => n.SentAt)
            .ToListAsync(cancellationToken);
}
