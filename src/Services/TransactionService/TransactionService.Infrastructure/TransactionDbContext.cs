using Microsoft.EntityFrameworkCore;
using TransactionService.Domain;

namespace TransactionService.Infrastructure;

public sealed class TransactionDbContext(DbContextOptions<TransactionDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Transaction>();
        entity.HasKey(t => t.Id);
        entity.Property(t => t.Category).IsRequired().HasMaxLength(100);
        entity.Property(t => t.Currency).IsRequired().HasMaxLength(3);
        entity.Property(t => t.Amount).HasColumnType("decimal(18,2)");
        entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        entity.HasIndex(t => t.CompanyId);
    }
}
