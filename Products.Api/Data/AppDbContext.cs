using Microsoft.EntityFrameworkCore;
using Products.Api.Models;

namespace Products.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Description)
                .HasMaxLength(2000);

            entity.Property(e => e.Price)
                .HasPrecision(18, 2);

            entity.Property(e => e.Category)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(e => e.Category);
        });

        modelBuilder.Entity<Product>()
            .HasQueryFilter(e => !e.IsDeleted);
    }
}
