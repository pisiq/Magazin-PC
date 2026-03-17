using Microsoft.EntityFrameworkCore;
using Recomandare_PC.Models;

namespace Recomandare_PC.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Subcategory> Subcategories => Set<Subcategory>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Relationships
        modelBuilder.Entity<Subcategory>()
            .HasOne(s => s.Category)
            .WithMany(c => c.Subcategories)
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Product>()
            .HasOne(p => p.Subcategory)
            .WithMany(s => s.Products)
            .HasForeignKey(p => p.SubcategoryId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // Seed data
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "CPU" },
            new Category { Id = 2, Name = "GPU" },
            new Category { Id = 3, Name = "RAM" },
            new Category { Id = 4, Name = "Motherboard" },
            new Category { Id = 5, Name = "PSU" },
            new Category { Id = 6, Name = "Storage" },
            new Category { Id = 7, Name = "Cooler" },
            new Category { Id = 8, Name = "Case" }
        );

        modelBuilder.Entity<Subcategory>().HasData(
            // Cooler subtypes
            new Subcategory { Id = 1, CategoryId = 7, Name = "Aer" },
            new Subcategory { Id = 2, CategoryId = 7, Name = "Lichid" },
            // Storage subtypes
            new Subcategory { Id = 3, CategoryId = 6, Name = "SSD NVMe" },
            new Subcategory { Id = 4, CategoryId = 6, Name = "SSD SATA" },
            new Subcategory { Id = 5, CategoryId = 6, Name = "HDD" },
            // RAM subtypes
            new Subcategory { Id = 6, CategoryId = 3, Name = "DDR4" },
            new Subcategory { Id = 7, CategoryId = 3, Name = "DDR5" }
        );
    }
}
