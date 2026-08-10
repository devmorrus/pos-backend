using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public class TaxRuleConfiguration : IEntityTypeConfiguration<TaxRule>
{
    public void Configure(EntityTypeBuilder<TaxRule> builder)
    {
        builder.ToTable("tax_rules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Rate).HasColumnType("decimal(8,4)");
        builder.HasIndex(x => new { x.OutletId, x.IsActive, x.UpdatedAt });

        builder.HasOne(x => x.Outlet)
            .WithMany(o => o.TaxRules)
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ServiceChargeRuleConfiguration : IEntityTypeConfiguration<ServiceChargeRule>
{
    public void Configure(EntityTypeBuilder<ServiceChargeRule> builder)
    {
        builder.ToTable("service_charge_rules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Rate).HasColumnType("decimal(8,4)");
        builder.HasIndex(x => new { x.OutletId, x.IsActive, x.UpdatedAt });

        builder.HasOne(x => x.Outlet)
            .WithMany(o => o.ServiceChargeRules)
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PromoCampaignConfiguration : IEntityTypeConfiguration<PromoCampaign>
{
    public void Configure(EntityTypeBuilder<PromoCampaign> builder)
    {
        builder.ToTable("promo_campaigns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.DiscountType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.DiscountValue).HasColumnType("decimal(14,2)");
        builder.Property(x => x.ScopeType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.MinimumSpend).HasColumnType("decimal(14,2)");
        builder.Property(x => x.MaximumDiscountAmount).HasColumnType("decimal(14,2)");
        builder.HasIndex(x => new { x.OutletId, x.IsActive, x.StartAt, x.EndAt });
        builder.HasIndex(x => new { x.OutletId, x.Code }).IsUnique().HasFilter("\"Code\" IS NOT NULL");

        builder.HasOne(x => x.Outlet)
            .WithMany(o => o.PromoCampaigns)
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PromoCampaignTargetConfiguration : IEntityTypeConfiguration<PromoCampaignTarget>
{
    public void Configure(EntityTypeBuilder<PromoCampaignTarget> builder)
    {
        builder.ToTable("promo_campaign_targets");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.PromoCampaignId, x.ProductId, x.CategoryId });

        builder.HasOne(x => x.PromoCampaign)
            .WithMany(p => p.Targets)
            .HasForeignKey(x => x.PromoCampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("vouchers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.DiscountType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.DiscountValue).HasColumnType("decimal(14,2)");
        builder.Property(x => x.MinimumSpend).HasColumnType("decimal(14,2)");
        builder.Property(x => x.MaximumDiscountAmount).HasColumnType("decimal(14,2)");
        builder.HasIndex(x => new { x.OutletId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.OutletId, x.IsActive, x.StartAt, x.EndAt });

        builder.HasOne(x => x.Outlet)
            .WithMany(o => o.Vouchers)
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VoucherRedemptionConfiguration : IEntityTypeConfiguration<VoucherRedemption>
{
    public void Configure(EntityTypeBuilder<VoucherRedemption> builder)
    {
        builder.ToTable("voucher_redemptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RedeemedAmount).HasColumnType("decimal(14,2)");
        builder.HasIndex(x => x.TransactionId).IsUnique();
        builder.HasIndex(x => new { x.VoucherId, x.RedeemedAt });

        builder.HasOne(x => x.Voucher)
            .WithMany(v => v.Redemptions)
            .HasForeignKey(x => x.VoucherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Transaction)
            .WithMany(t => t.VoucherRedemptions)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
