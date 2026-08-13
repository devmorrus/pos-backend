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

        // Unique constraints to support both parent stock and variant stock
        builder.HasIndex(x => new { x.ProductId, x.OutletId })
            .IsUnique()
            .HasFilter("\"ProductVariantId\" IS NULL");

        builder.HasIndex(x => new { x.ProductId, x.ProductVariantId, x.OutletId })
            .IsUnique()
            .HasFilter("\"ProductVariantId\" IS NOT NULL");

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

public class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("stock_transfers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TransferNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.TransferNumber).IsUnique();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();

        builder.HasOne(x => x.FromOutlet)
            .WithMany()
            .HasForeignKey(x => x.FromOutletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToOutlet)
            .WithMany()
            .HasForeignKey(x => x.ToOutletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RequestedByUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockTransferItemConfiguration : IEntityTypeConfiguration<StockTransferItem>
{
    public void Configure(EntityTypeBuilder<StockTransferItem> builder)
    {
        builder.ToTable("stock_transfer_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Qty).HasColumnType("decimal(12,2)");

        builder.HasOne(x => x.StockTransfer)
            .WithMany(t => t.Items)
            .HasForeignKey(x => x.StockTransferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
