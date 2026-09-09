using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public class GoBizIntegrationConfiguration : IEntityTypeConfiguration<GoBizIntegration>
{
    public void Configure(EntityTypeBuilder<GoBizIntegration> builder)
    {
        builder.ToTable("gobiz_integrations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalMerchantId).HasMaxLength(150);
        builder.Property(x => x.AccessToken).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.RefreshToken).HasMaxLength(4000);
        builder.Property(x => x.TokenType).HasMaxLength(50);
        builder.Property(x => x.Scope).HasMaxLength(500);
        builder.Property(x => x.RawMetadataJson).HasColumnType("text");
        builder.HasIndex(x => x.OutletId).IsUnique();

        builder.HasOne(x => x.Outlet)
            .WithMany()
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class GoBizOAuthStateConfiguration : IEntityTypeConfiguration<GoBizOAuthState>
{
    public void Configure(EntityTypeBuilder<GoBizOAuthState> builder)
    {
        builder.ToTable("gobiz_oauth_states");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.State).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.State).IsUnique();
        builder.HasIndex(x => new { x.OutletId, x.ExpiresAtUtc });

        builder.HasOne(x => x.Outlet)
            .WithMany()
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class GoBizDirectIntegrationConfiguration : IEntityTypeConfiguration<GoBizDirectIntegration>
{
    public void Configure(EntityTypeBuilder<GoBizDirectIntegration> builder)
    {
        builder.ToTable("gobiz_direct_integrations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PartnerId).HasMaxLength(150).IsRequired();
        builder.Property(x => x.GoBizOutletId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Environment).HasMaxLength(50).IsRequired();
        builder.Property(x => x.LastCatalogSyncStatus).HasMaxLength(50);
        builder.Property(x => x.LastCatalogSyncMessage).HasMaxLength(1000);
        builder.HasIndex(x => x.OutletId).IsUnique();
        builder.HasIndex(x => x.GoBizOutletId).IsUnique();

        builder.HasOne(x => x.Outlet)
            .WithMany()
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class GoBizProductMappingConfiguration : IEntityTypeConfiguration<GoBizProductMapping>
{
    public void Configure(EntityTypeBuilder<GoBizProductMapping> builder)
    {
        builder.ToTable("gobiz_product_mappings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GoBizItemId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.GoBizCategoryId).HasMaxLength(200);
        builder.Property(x => x.LastSyncError).HasMaxLength(1000);
        builder.HasIndex(x => new { x.OutletId, x.ProductId, x.ProductVariantId }).IsUnique();
        builder.HasIndex(x => x.GoBizItemId);

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
    }
}

public class GoBizClientConfigConfiguration : IEntityTypeConfiguration<GoBizClientConfig>
{
    public void Configure(EntityTypeBuilder<GoBizClientConfig> builder)
    {
        builder.ToTable("gobiz_client_configs");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.BusinessId).IsUnique();
        builder.Property(x => x.Environment).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ClientId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ClientSecretProtected).HasColumnType("text").IsRequired();
        builder.Property(x => x.PartnerId).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AuthorizationUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.TokenUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ApiBaseUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RedirectUri).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Scope).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.UserType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Prompt).HasMaxLength(50).IsRequired();
        builder.Property(x => x.WebhookSecretProtected).HasColumnType("text");

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class GoBizOrderInboxConfiguration : IEntityTypeConfiguration<GoBizOrderInbox>
{
    public void Configure(EntityTypeBuilder<GoBizOrderInbox> builder)
    {
        builder.ToTable("gobiz_order_inboxes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GoBizOrderId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RawPayloadJson).HasColumnType("text").IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.HasIndex(x => x.GoBizOrderId).IsUnique();
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.Outlet)
            .WithMany()
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Transaction)
            .WithMany()
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
