using Microsoft.EntityFrameworkCore;

namespace MiniDpp.Data;

public sealed class DppDbContext(DbContextOptions<DppDbContext> options) : DbContext(options)
{
    public DbSet<ProductModel> Models => Set<ProductModel>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductModel>(entity =>
        {
            entity.ToTable("Models");
            entity.Property(model => model.Name).IsRequired();
            entity.HasIndex(model => model.Name).IsUnique();
        });
        modelBuilder.Entity<Batch>(entity =>
        {
            entity.Property(batch => batch.Number).IsRequired();
            entity.Property(batch => batch.ProductionLocation).IsRequired();
            entity.HasIndex(batch => new { batch.ModelId, batch.Number }).IsUnique();
            entity.HasOne(batch => batch.Model).WithMany().HasForeignKey(batch => batch.ModelId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(product => product.Gtin).HasColumnType("TEXT").IsRequired();
            entity.HasIndex(product => product.Gtin).IsUnique();
            entity.HasOne(product => product.Batch).WithMany().HasForeignKey(product => product.BatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("Products", table => table.HasCheckConstraint("CK_Products_Gtin",
                "length(Gtin) = 14 AND Gtin NOT GLOB '*[^0-9]*'"));
        });
    }
}
