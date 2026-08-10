using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Sku).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Sku).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Barcode).HasMaxLength(50);
        builder.HasIndex(x => x.Barcode).IsUnique().HasFilter("\"Barcode\" IS NOT NULL");
        builder.Property(x => x.BasePrice).HasColumnType("decimal(14,2)");
        builder.Property(x => x.CostPrice).HasColumnType("decimal(14,2)");
        builder.Property(x => x.Unit).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.IsTaxable);
        builder.Property(x => x.IsServiceChargeable);
        builder.Property(x => x.Version).IsRowVersion();

        builder.HasOne(x => x.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TransactionNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.TransactionNumber).IsUnique();
        builder.Property(x => x.Channel).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();

        foreach (var prop in new[]
                 {
                     nameof(Transaction.Subtotal),
                     nameof(Transaction.DiscountTotal),
                     nameof(Transaction.ManualDiscountTotal),
                     nameof(Transaction.PromoDiscountTotal),
                     nameof(Transaction.VoucherDiscountTotal),
                     nameof(Transaction.ServiceChargeTotal),
                     nameof(Transaction.TaxTotal),
                     nameof(Transaction.GrandTotal)
                 })
        {
            builder.Property(prop).HasColumnType("decimal(14,2)");
        }
        builder.Property(x => x.AppliedVoucherCode).HasMaxLength(100);
        builder.Property(x => x.AppliedPromoName).HasMaxLength(150);

        // Index untuk query dashboard/laporan per outlet & tanggal — dipakai sejak Fase 6
        builder.HasIndex(x => new { x.OutletId, x.CreatedAt });

        builder.HasOne(x => x.Outlet)
            .WithMany(o => o.Transactions)
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany(u => u.Transactions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.VoidedByUser)
            .WithMany()
            .HasForeignKey(x => x.VoidedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CashierSession)
            .WithMany(c => c.Transactions)
            .HasForeignKey(x => x.CashierSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TransactionItemConfiguration : IEntityTypeConfiguration<TransactionItem>
{
    public void Configure(EntityTypeBuilder<TransactionItem> builder)
    {
        builder.ToTable("transaction_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Qty).HasColumnType("decimal(10,2)");
        builder.Property(x => x.UnitPrice).HasColumnType("decimal(14,2)");
        builder.Property(x => x.UnitCost).HasColumnType("decimal(14,2)");
        builder.Property(x => x.DiscountAmount).HasColumnType("decimal(14,2)");
        builder.Property(x => x.LineTotal).HasColumnType("decimal(14,2)");

        builder.HasOne(x => x.Transaction)
            .WithMany(t => t.Items)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany(p => p.TransactionItems)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Method).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(14,2)");
        builder.Property(x => x.ReferenceNumber).HasMaxLength(100);

        builder.HasOne(x => x.Transaction)
            .WithMany(t => t.Payments)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReturnConfiguration : IEntityTypeConfiguration<Return>
{
    public void Configure(EntityTypeBuilder<Return> builder)
    {
        builder.ToTable("returns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Qty).HasColumnType("decimal(10,2)");
        builder.Property(x => x.RefundMethod).HasMaxLength(20).IsRequired();

        builder.HasOne(x => x.Transaction)
            .WithMany(t => t.Returns)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TransactionItem)
            .WithMany()
            .HasForeignKey(x => x.TransactionItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProcessedByUser)
            .WithMany()
            .HasForeignKey(x => x.ProcessedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CashierSessionConfiguration : IEntityTypeConfiguration<CashierSession>
{
    public void Configure(EntityTypeBuilder<CashierSession> builder)
    {
        builder.ToTable("cashier_sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.OpeningCash).HasColumnType("decimal(14,2)");
        builder.Property(x => x.ExpectedCash).HasColumnType("decimal(14,2)");
        builder.Property(x => x.ActualCash).HasColumnType("decimal(14,2)");
        builder.Property(x => x.Variance).HasColumnType("decimal(14,2)");

        builder.HasOne(x => x.Outlet)
            .WithMany()
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
