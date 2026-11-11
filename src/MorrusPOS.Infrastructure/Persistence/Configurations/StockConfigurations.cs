using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public class InventoryStockConfiguration : IEntityTypeConfiguration<InventoryStock>
{
    public void Configure(EntityTypeBuilder<InventoryStock> builder)
    {
        builder.ToTable("inventory_stock", t => t.HasCheckConstraint(
            "CK_inventory_stock_NonNegative", "\"QtyOnHand\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QtyOnHand).HasColumnType("decimal(12,2)");
        builder.Property(x => x.MinStockAlert).HasColumnType("decimal(12,2)");

        // Unique constraints to support both parent stock and variant stock
        builder.HasIndex(x => new { x.BusinessId, x.ProductId, x.StockLocationId })
            .IsUnique()
            .HasFilter("\"ProductVariantId\" IS NULL");

        builder.HasIndex(x => new { x.BusinessId, x.ProductId, x.ProductVariantId, x.StockLocationId })
            .IsUnique()
            .HasFilter("\"ProductVariantId\" IS NOT NULL");

        builder.HasOne(x => x.Product)
            .WithMany(p => p.InventoryStocks)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StockLocation)
            .WithMany()
            .HasForeignKey(x => x.StockLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ChannelStockPolicyConfiguration : IEntityTypeConfiguration<ChannelStockPolicy>
{
    public void Configure(EntityTypeBuilder<ChannelStockPolicy> builder)
    {
        builder.ToTable("channel_stock_policies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BufferQty).HasColumnType("decimal(12,2)");
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);

        // Satu policy per outlet + produk (tanpa varian)
        builder.HasIndex(x => new { x.OutletId, x.ProductId })
            .IsUnique()
            .HasFilter("\"ProductVariantId\" IS NULL");

        // Satu policy per outlet + produk + varian
        builder.HasIndex(x => new { x.OutletId, x.ProductId, x.ProductVariantId })
            .IsUnique()
            .HasFilter("\"ProductVariantId\" IS NOT NULL");

        builder.HasIndex(x => x.OutletId);
        builder.HasIndex(x => x.ProductId);

        builder.HasOne(x => x.Outlet)
            .WithMany()
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UpdatedByUser)
            .WithMany()
            .HasForeignKey(x => x.UpdatedBy)
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
        builder.Property(x => x.PostingKey).HasMaxLength(300).IsRequired();
        builder.ToTable("stock_ledger", t => t.HasCheckConstraint(
            "CK_stock_ledger_TrackingReference", "NOT (\"ProductBatchId\" IS NOT NULL AND \"ProductSerialId\" IS NOT NULL)"));
        builder.ToTable("stock_ledger", t => t.HasCheckConstraint(
            "CK_stock_ledger_QtyNonZero", "\"QtyChange\" <> 0"));
        builder.ToTable("stock_ledger", t => t.HasCheckConstraint(
            "CK_stock_ledger_MovementType", "\"MovementType\" IN ('opening_balance','sale','return','purchase_in','transfer_in','transfer_out','opname_adjustment','consignment_in','consignment_return')"));
        builder.ToTable("stock_ledger", t => t.HasCheckConstraint(
            "CK_stock_ledger_MovementDirection", "(\"MovementType\" IN ('sale','transfer_out','consignment_return') AND \"QtyChange\" < 0) OR (\"MovementType\" IN ('return','purchase_in','transfer_in','consignment_in') AND \"QtyChange\" > 0) OR \"MovementType\" IN ('opening_balance','opname_adjustment')"));
        builder.ToTable("stock_ledger", t => t.HasCheckConstraint(
            "CK_stock_ledger_SerialQty", "\"ProductSerialId\" IS NULL OR abs(\"QtyChange\") = 1"));

        // ReferenceId SENGAJA tidak dikonfigurasi sebagai FK — polimorfik,
        // bisa merujuk ke transactions/purchase_orders/stock_transfers/stock_opnames.

        builder.HasIndex(x => new { x.ProductId, x.StockLocationId }); // dipakai untuk rekalkulasi & laporan
        builder.HasIndex(x => new { x.BusinessId, x.PostingKey }).IsUnique();
        builder.HasIndex(x => new { x.BusinessId, x.StockLocationId, x.ProductId, x.ProductVariantId, x.CreatedAt, x.Id });

        builder.HasOne(x => x.Product)
            .WithMany(p => p.StockLedgers)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StockLocation)
            .WithMany()
            .HasForeignKey(x => x.StockLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductBatch).WithMany()
            .HasForeignKey(x => x.ProductBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProductSerial).WithMany()
            .HasForeignKey(x => x.ProductSerialId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockReconciliationRunConfiguration : IEntityTypeConfiguration<StockReconciliationRun>
{
    public void Configure(EntityTypeBuilder<StockReconciliationRun> builder)
    {
        builder.ToTable("stock_reconciliation_runs", t =>
        {
            t.HasCheckConstraint("CK_stock_reconciliation_runs_Mode", "\"Mode\" IN ('preview','repair')");
            t.HasCheckConstraint("CK_stock_reconciliation_runs_Status", "\"Status\" IN ('running','completed','failed','blocked')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Mode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.ErrorCode).HasMaxLength(80);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.HasIndex(x => new { x.BusinessId, x.StockLocationId, x.StartedAt });
        builder.HasOne(x => x.StockLocation).WithMany().HasForeignKey(x => x.StockLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RequestedByUser).WithMany().HasForeignKey(x => x.RequestedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockReconciliationItemConfiguration : IEntityTypeConfiguration<StockReconciliationItem>
{
    public void Configure(EntityTypeBuilder<StockReconciliationItem> builder)
    {
        builder.ToTable("stock_reconciliation_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TrackingMode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.LedgerQty).HasColumnType("decimal(12,2)");
        builder.Property(x => x.StoredQty).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Difference).HasColumnType("decimal(12,2)");
        builder.Property(x => x.BatchQty).HasColumnType("decimal(12,2)");
        builder.Property(x => x.IssueType).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.BusinessId, x.RunId, x.IssueType });
        builder.HasIndex(x => new { x.BusinessId, x.StockLocationId, x.ProductId, x.ProductVariantId });
        builder.HasOne(x => x.Run).WithMany(x => x.Items).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.StockLocation).WithMany().HasForeignKey(x => x.StockLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockOpnameConfiguration : IEntityTypeConfiguration<StockOpname>
{
    public void Configure(EntityTypeBuilder<StockOpname> builder)
    {
        builder.ToTable("stock_opnames");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();

        builder.HasOne(x => x.StockLocation)
            .WithMany()
            .HasForeignKey(x => x.StockLocationId)
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
        builder.Property(x => x.UnitCostSnapshot).HasColumnType("decimal(14,2)");

        builder.HasOne(x => x.StockOpname)
            .WithMany(o => o.Items)
            .HasForeignKey(x => x.StockOpnameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductBatch).WithMany()
            .HasForeignKey(x => x.ProductBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProductSerial).WithMany()
            .HasForeignKey(x => x.ProductSerialId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductBatchConfiguration : IEntityTypeConfiguration<ProductBatch>
{
    public void Configure(EntityTypeBuilder<ProductBatch> builder)
    {
        builder.ToTable("ProductBatches", t =>
        {
            t.HasCheckConstraint("CK_ProductBatches_Qty", "\"QtyProduction\" >= 0 AND \"QtyRemaining\" >= 0 AND \"QtyRemaining\" <= \"QtyProduction\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BatchNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.QtyProduction).HasColumnType("decimal(12,2)");
        builder.Property(x => x.QtyRemaining).HasColumnType("decimal(12,2)");
        builder.HasOne(x => x.StockLocation).WithMany().HasForeignKey(x => x.StockLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BusinessId, x.StockLocationId, x.ProductId, x.BatchNumber })
            .IsUnique().HasFilter("\"ProductVariantId\" IS NULL");
        builder.HasIndex(x => new { x.BusinessId, x.StockLocationId, x.ProductId, x.ProductVariantId, x.BatchNumber })
            .IsUnique().HasFilter("\"ProductVariantId\" IS NOT NULL");
        builder.HasIndex(x => new { x.StockLocationId, x.ProductId, x.ExpiryDate });
    }
}

public class ProductSerialConfiguration : IEntityTypeConfiguration<ProductSerial>
{
    public void Configure(EntityTypeBuilder<ProductSerial> builder)
    {
        builder.ToTable("product_serials", t => t.HasCheckConstraint(
            "CK_product_serials_Status", "\"Status\" IN ('available','reserved','sold','in_transfer','returned_to_supplier','written_off')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SerialNumber).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NormalizedSerialNumber).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.BusinessId, x.NormalizedSerialNumber }).IsUnique();
        builder.HasIndex(x => new { x.BusinessId, x.StockLocationId, x.ProductId, x.ProductVariantId, x.Status });
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StockLocation).WithMany().HasForeignKey(x => x.StockLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReceivingNoteItem).WithMany().HasForeignKey(x => x.ReceivingNoteItemId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("stock_reservations", t =>
        {
            t.HasCheckConstraint("CK_stock_reservations_Status", "\"Status\" IN ('active','consumed','released','expired')");
            t.HasCheckConstraint("CK_stock_reservations_Source", "\"SourceType\" IN ('pos','channel_order')");
            t.HasCheckConstraint("CK_stock_reservations_Lifecycle",
                "(\"Status\" = 'active' AND \"ResolvedAt\" IS NULL AND \"ConsumedByTransactionId\" IS NULL) OR " +
                "(\"Status\" = 'consumed' AND \"ResolvedAt\" IS NOT NULL AND \"ConsumedByTransactionId\" IS NOT NULL) OR " +
                "(\"Status\" IN ('released','expired') AND \"ResolvedAt\" IS NOT NULL AND \"ConsumedByTransactionId\" IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.ReleaseReason).HasMaxLength(200);
        builder.HasIndex(x => new { x.BusinessId, x.Status, x.ExpiresAt });
        builder.HasIndex(x => new { x.BusinessId, x.StockLocationId, x.Status, x.ExpiresAt });
        builder.HasIndex(x => new { x.BusinessId, x.CashierSessionId, x.Status });
        builder.HasOne(x => x.StockLocation).WithMany().HasForeignKey(x => x.StockLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CashierSession).WithMany().HasForeignKey(x => x.CashierSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Register).WithMany().HasForeignKey(x => x.RegisterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ConsumedByTransaction).WithMany().HasForeignKey(x => x.ConsumedByTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockReservationItemConfiguration : IEntityTypeConfiguration<StockReservationItem>
{
    public void Configure(EntityTypeBuilder<StockReservationItem> builder)
    {
        builder.ToTable("stock_reservation_items", t =>
        {
            t.HasCheckConstraint("CK_stock_reservation_items_Status", "\"Status\" IN ('active','consumed','released','expired')");
            t.HasCheckConstraint("CK_stock_reservation_items_Qty", "\"Qty\" > 0");
            t.HasCheckConstraint("CK_stock_reservation_items_TrackingMode", "\"TrackingModeSnapshot\" IN ('quantity','batch','serial')");
            t.HasCheckConstraint("CK_stock_reservation_items_TrackingReference",
                "(\"TrackingModeSnapshot\" = 'quantity' AND \"ProductBatchId\" IS NULL AND \"ProductSerialId\" IS NULL) OR " +
                "(\"TrackingModeSnapshot\" = 'batch' AND \"ProductBatchId\" IS NOT NULL AND \"ProductSerialId\" IS NULL) OR " +
                "(\"TrackingModeSnapshot\" = 'serial' AND \"ProductBatchId\" IS NULL AND \"ProductSerialId\" IS NOT NULL AND \"Qty\" = 1)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Qty).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TrackingModeSnapshot).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.BusinessId, x.StockLocationId, x.ProductId, x.ProductVariantId, x.Status });
        builder.HasIndex(x => new { x.BusinessId, x.ProductBatchId, x.Status });
        builder.HasIndex(x => new { x.BusinessId, x.ProductSerialId, x.Status });
        builder.HasOne(x => x.StockReservation).WithMany(x => x.Items).HasForeignKey(x => x.StockReservationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.StockLocation).WithMany().HasForeignKey(x => x.StockLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProductBatch).WithMany().HasForeignKey(x => x.ProductBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProductSerial).WithMany().HasForeignKey(x => x.ProductSerialId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("stock_transfers", t => t.HasCheckConstraint(
            "CK_stock_transfers_Status", "\"Status\" IN ('in_transit','received','received_with_variance','returned','cancelled','pending','approved','rejected')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TransferNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.BusinessId, x.TransferNumber }).IsUnique();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReceiptNote).HasMaxLength(500);
        builder.HasIndex(x => new { x.BusinessId, x.FromStockLocationId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.BusinessId, x.ToStockLocationId, x.Status, x.CreatedAt });

        builder.HasOne(x => x.FromStockLocation)
            .WithMany()
            .HasForeignKey(x => x.FromStockLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToStockLocation)
            .WithMany()
            .HasForeignKey(x => x.ToStockLocationId)
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
        builder.ToTable("stock_transfer_items", t =>
        {
            t.HasCheckConstraint("CK_stock_transfer_items_TrackingReference",
                "NOT (\"ProductBatchId\" IS NOT NULL AND \"ProductSerialId\" IS NOT NULL)");
            t.HasCheckConstraint("CK_stock_transfer_items_Quantities",
                "\"Qty\" > 0 AND (\"ReceivedQty\" IS NULL OR (\"ReceivedQty\" >= 0 AND \"DifferenceQty\" = \"ReceivedQty\" - \"Qty\"))");
            t.HasCheckConstraint("CK_stock_transfer_items_Variance",
                "\"ReceivedQty\" IS NULL OR (\"DifferenceQty\" = 0 AND \"VarianceReasonCode\" IS NULL) OR (\"DifferenceQty\" <> 0 AND \"VarianceReasonCode\" IS NOT NULL AND \"VarianceNote\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Qty).HasColumnType("decimal(12,2)");
        builder.Property(x => x.ReceivedQty).HasColumnType("decimal(12,2)");
        builder.Property(x => x.DifferenceQty).HasColumnType("decimal(12,2)");
        builder.Property(x => x.VarianceReasonCode).HasMaxLength(30);
        builder.Property(x => x.VarianceNote).HasMaxLength(500);
        builder.Property(x => x.UnitCostSnapshot).HasColumnType("decimal(14,2)");

        builder.HasOne(x => x.StockTransfer)
            .WithMany(t => t.Items)
            .HasForeignKey(x => x.StockTransferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductBatch)
            .WithMany()
            .HasForeignKey(x => x.ProductBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductSerial)
            .WithMany()
            .HasForeignKey(x => x.ProductSerialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
