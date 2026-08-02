using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public class ChannelAccountConfiguration : IEntityTypeConfiguration<ChannelAccount>
{
    public void Configure(EntityTypeBuilder<ChannelAccount> builder)
    {
        builder.ToTable("channel_accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ChannelName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.MerchantId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ApiKey).HasMaxLength(250);

        builder.HasOne(x => x.Outlet)
            .WithMany()
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ChannelSettlementConfiguration : IEntityTypeConfiguration<ChannelSettlement>
{
    public void Configure(EntityTypeBuilder<ChannelSettlement> builder)
    {
        builder.ToTable("channel_settlements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SettlementNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.SettlementNumber).IsUnique();
        builder.Property(x => x.GrossAmount).HasColumnType("decimal(14,2)");
        builder.Property(x => x.CommissionAmount).HasColumnType("decimal(14,2)");
        builder.Property(x => x.NetAmount).HasColumnType("decimal(14,2)");
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();

        builder.HasOne(x => x.ChannelAccount)
            .WithMany()
            .HasForeignKey(x => x.ChannelAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ChannelSettlementItemConfiguration : IEntityTypeConfiguration<ChannelSettlementItem>
{
    public void Configure(EntityTypeBuilder<ChannelSettlementItem> builder)
    {
        builder.ToTable("channel_settlement_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GrossAmount).HasColumnType("decimal(14,2)");
        builder.Property(x => x.CommissionAmount).HasColumnType("decimal(14,2)");
        builder.Property(x => x.NetAmount).HasColumnType("decimal(14,2)");

        builder.HasOne(x => x.ChannelSettlement)
            .WithMany(s => s.Items)
            .HasForeignKey(x => x.ChannelSettlementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Transaction)
            .WithMany()
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class IntegrationLogConfiguration : IEntityTypeConfiguration<IntegrationLog>
{
    public void Configure(EntityTypeBuilder<IntegrationLog> builder)
    {
        builder.ToTable("integration_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StatusCode).HasMaxLength(10);
    }
}
