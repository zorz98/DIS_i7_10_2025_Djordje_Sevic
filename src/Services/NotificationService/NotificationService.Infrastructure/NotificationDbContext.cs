using Microsoft.EntityFrameworkCore;
using NotificationService.Domain;

namespace NotificationService.Infrastructure;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Notification>();
        entity.HasKey(n => n.Id);
        entity.Property(n => n.Message).IsRequired().HasMaxLength(500);
        entity.HasIndex(n => n.CompanyId);
    }
}
