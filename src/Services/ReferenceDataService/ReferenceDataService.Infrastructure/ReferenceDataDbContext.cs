using Microsoft.EntityFrameworkCore;
using ReferenceDataService.Domain;

namespace ReferenceDataService.Infrastructure;

public sealed class ReferenceDataDbContext(DbContextOptions<ReferenceDataDbContext> options)
    : DbContext(options)
{
    public DbSet<EmissionFactor> EmissionFactors => Set<EmissionFactor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<EmissionFactor>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
        entity.HasIndex(e => e.Category).IsUnique();
        entity.Property(e => e.Co2FactorPerEur).HasColumnType("decimal(18,4)");

        entity.HasData(
            new EmissionFactor { Id = 1, Category = "Fuel", Co2FactorPerEur = 2.31m },
            new EmissionFactor { Id = 2, Category = "Electricity", Co2FactorPerEur = 0.45m },
            new EmissionFactor { Id = 3, Category = "Flights", Co2FactorPerEur = 2.50m },
            new EmissionFactor { Id = 4, Category = "PublicTransport", Co2FactorPerEur = 0.10m },
            new EmissionFactor { Id = 5, Category = "OfficeSupplies", Co2FactorPerEur = 0.20m });
    }
}
