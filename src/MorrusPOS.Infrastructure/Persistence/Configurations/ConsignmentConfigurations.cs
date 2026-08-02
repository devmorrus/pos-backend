using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public class ConsignmentConfiguration : IEntityTypeConfiguration<Consignment>
{
    public void Configure(EntityTypeBuilder<Consignment> builder)
    {
        builder.ToTable("consignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ConsignmentNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.ConsignmentNumber).IsUnique();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();

        builder.HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
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

public class ConsignmentItemConfiguration : IEntityTypeConfiguration<ConsignmentItem>
{
    public void Configure(EntityTypeBuilder<ConsignmentItem> builder)
    {
        builder.ToTable("consignment_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Qty).HasColumnType("decimal(12,2)");
        builder.Property(x => x.UnitCost).HasColumnType("decimal(14,2)");
        builder.Property(x => x.UnitPrice).HasColumnType("decimal(14,2)");

        builder.HasOne(x => x.Consignment)
            .WithMany(c => c.Items)
            .HasForeignKey(x => x.ConsignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConsignmentSaleConfiguration : IEntityTypeConfiguration<ConsignmentSale>
{
    public void Configure(EntityTypeBuilder<ConsignmentSale> builder)
    {
        builder.ToTable("consignment_sales");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Qty).HasColumnType("decimal(12,2)");
        builder.Property(x => x.UnitCost).HasColumnType("decimal(14,2)");
        builder.Property(x => x.TotalAmount).HasColumnType("decimal(14,2)");
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();

        builder.HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TransactionItem)
            .WithMany()
            .HasForeignKey(x => x.TransactionItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ConsignmentSettlement)
            .WithMany(s => s.Sales)
            .HasForeignKey(x => x.ConsignmentSettlementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConsignmentSettlementConfiguration : IEntityTypeConfiguration<ConsignmentSettlement>
{
    public void Configure(EntityTypeBuilder<ConsignmentSettlement> builder)
    {
        builder.ToTable("consignment_settlements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SettlementNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.SettlementNumber).IsUnique();
        builder.Property(x => x.TotalAmount).HasColumnType("decimal(14,2)");
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();

        builder.HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
