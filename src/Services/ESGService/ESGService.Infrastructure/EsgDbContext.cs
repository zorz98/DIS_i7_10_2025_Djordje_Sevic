using Microsoft.EntityFrameworkCore;
using ESGService.Domain;

namespace ESGService.Infrastructure;

public sealed class EsgDbContext(DbContextOptions<EsgDbContext> options) : DbContext(options)
{
    public DbSet<EsgResult> EsgResults => Set<EsgResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<EsgResult>();
        entity.HasKey(r => r.TransactionId);
        entity.Property(r => r.Category).IsRequired().HasMaxLength(100);
        entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(30);
        entity.Property(r => r.Co2Kg).HasColumnType("decimal(18,4)");
        entity.HasIndex(r => r.CompanyId);
    }
}
