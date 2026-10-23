using AvancoChaoFabrica.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AvancoChaoFabrica.Api.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<ProductionItem> ProductionItems => Set<ProductionItem>();
    public DbSet<OrderStageWeight> OrderStageWeights => Set<OrderStageWeight>();
    public DbSet<ItemStageProgress> ItemStageProgress => Set<ItemStageProgress>();
    public DbSet<ShipmentLine> ShipmentLines => Set<ShipmentLine>();
    public DbSet<LaborAllocation> LaborAllocations => Set<LaborAllocation>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportedRow> ImportedRows => Set<ImportedRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().HasIndex(x => x.Name);
        modelBuilder.Entity<Supplier>().HasIndex(x => x.Name);
        modelBuilder.Entity<Project>()
            .HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProductionOrder>()
            .HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProductionOrder>()
            .HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProductionItem>()
            .HasOne(x => x.ProductionOrder)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.ProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProductionItem>()
            .HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OrderStageWeight>()
            .HasIndex(x => new { x.ProductionOrderId, x.StageCode })
            .IsUnique();
        modelBuilder.Entity<ItemStageProgress>()
            .HasIndex(x => new { x.ProductionItemId, x.StageCode, x.ReportDate });
        modelBuilder.Entity<ProductionItem>().Property(x => x.Quantity).HasPrecision(18, 3);
        modelBuilder.Entity<ProductionItem>().Property(x => x.UnitWeightKg).HasPrecision(18, 3);
        modelBuilder.Entity<ProductionItem>().Property(x => x.TotalWeightKg).HasPrecision(18, 3);
        modelBuilder.Entity<OrderStageWeight>().Property(x => x.WeightPercent).HasPrecision(7, 4);
        modelBuilder.Entity<ItemStageProgress>().Property(x => x.CompletionPercent).HasPrecision(7, 4);
        modelBuilder.Entity<ItemStageProgress>().Property(x => x.CompletedWeightKg).HasPrecision(18, 3);
        modelBuilder.Entity<ShipmentLine>().Property(x => x.Quantity).HasPrecision(18, 3);
        modelBuilder.Entity<ShipmentLine>().Property(x => x.WeightKg).HasPrecision(18, 3);
        modelBuilder.Entity<LaborAllocation>()
            .HasOne(x => x.ProductionOrder)
            .WithMany()
            .HasForeignKey(x => x.ProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LaborAllocation>().Property(x => x.Hours).HasPrecision(8, 2);
    }
}
