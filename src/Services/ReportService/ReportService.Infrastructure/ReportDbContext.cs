using Microsoft.EntityFrameworkCore;
using ReportService.Domain;

namespace ReportService.Infrastructure;

public sealed class ReportDbContext(DbContextOptions<ReportDbContext> options) : DbContext(options)
{
    public DbSet<TransactionRecord> TransactionRecords => Set<TransactionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<TransactionRecord>();
        entity.HasKey(r => r.TransactionId);
        entity.Property(r => r.Category).IsRequired().HasMaxLength(100);
        entity.Property(r => r.Amount).HasColumnType("decimal(18,2)");
        entity.Property(r => r.Co2Kg).HasColumnType("decimal(18,4)");
        entity.HasIndex(r => new { r.CompanyId, r.Date });
    }
}
