using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public class InventoryStockConfiguration : IEntityTypeConfiguration<InventoryStock>
{
    public void Configure(EntityTypeBuilder<InventoryStock> builder)
    {
        builder.ToTable("inventory_stock");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QtyOnHand).HasColumnType("decimal(12,2)");
        builder.Property(x => x.MinStockAlert).HasColumnType("decimal(12,2)");

        // Unique constraint (product_id, outlet_id) — dipakai juga sebagai target
        // ON CONFLICT oleh trigger database di StockLedgerTrigger.sql
        builder.HasIndex(x => new { x.ProductId, x.OutletId }).IsUnique();

        builder.HasOne(x => x.Product)
            .WithMany(p => p.InventoryStocks)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Outlet)
            .WithMany(o => o.InventoryStocks)
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockLedgerConfiguration : IEntityTypeConfiguration<StockLedger>
{
    public void Configure(EntityTypeBuilder<StockLedger> builder)
    {
        builder.ToTable("stock_ledger");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MovementType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.QtyChange).HasColumnType("decimal(12,2)");
        builder.Property(x => x.ReferenceType).HasMaxLength(30).IsRequired();

        // ReferenceId SENGAJA tidak dikonfigurasi sebagai FK — polimorfik,
        // bisa merujuk ke transactions/purchase_orders/stock_transfers/stock_opnames.

        builder.HasIndex(x => new { x.ProductId, x.OutletId }); // dipakai untuk rekalkulasi & laporan

        builder.HasOne(x => x.Product)
            .WithMany(p => p.StockLedgers)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Outlet)
            .WithMany()
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockOpnameConfiguration : IEntityTypeConfiguration<StockOpname>
{
    public void Configure(EntityTypeBuilder<StockOpname> builder)
    {
        builder.ToTable("stock_opnames");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();

        builder.HasOne(x => x.Outlet)
            .WithMany()
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PerformedByUser)
            .WithMany()
            .HasForeignKey(x => x.PerformedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockOpnameItemConfiguration : IEntityTypeConfiguration<StockOpnameItem>
{
    public void Configure(EntityTypeBuilder<StockOpnameItem> builder)
    {
        builder.ToTable("stock_opname_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SystemQty).HasColumnType("decimal(12,2)");
        builder.Property(x => x.PhysicalQty).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Variance).HasColumnType("decimal(12,2)");

        builder.HasOne(x => x.StockOpname)
            .WithMany(o => o.Items)
            .HasForeignKey(x => x.StockOpnameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
