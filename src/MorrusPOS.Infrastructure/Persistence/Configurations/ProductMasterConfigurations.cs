using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public class ProductBrandConfiguration : IEntityTypeConfiguration<ProductBrand>
{
    public void Configure(EntityTypeBuilder<ProductBrand> b)
    {
        b.ToTable("product_brands");
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.BusinessId, x.Id });
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.BusinessId, x.NormalizedName }).IsUnique();
    }
}

public class ProductModelConfiguration : IEntityTypeConfiguration<ProductModel>
{
    public void Configure(EntityTypeBuilder<ProductModel> b)
    {
        b.ToTable("product_models");
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.BusinessId, x.BrandId, x.Id });
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.NormalizedName).HasMaxLength(120).IsRequired();
        b.HasIndex(x => new { x.BusinessId, x.BrandId, x.NormalizedName }).IsUnique();
        b.HasOne(x => x.Brand).WithMany().HasForeignKey(x => new { x.BusinessId, x.BrandId })
            .HasPrincipalKey(x => new { x.BusinessId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductIdentifierConfiguration : IEntityTypeConfiguration<ProductIdentifier>
{
    public void Configure(EntityTypeBuilder<ProductIdentifier> b)
    {
        b.ToTable("product_identifiers", t => t.HasCheckConstraint("CK_product_identifiers_Type", "\"IdentifierType\" IN ('sku','barcode')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.IdentifierType).HasMaxLength(20).IsRequired();
        b.Property(x => x.NormalizedValue).HasMaxLength(50).IsRequired();
        b.HasIndex(x => new { x.BusinessId, x.IdentifierType, x.NormalizedValue }).IsUnique();
        b.HasIndex(x => new { x.BusinessId, x.ProductId, x.IdentifierType }).IsUnique().HasFilter("\"ProductVariantId\" IS NULL");
        b.HasIndex(x => new { x.BusinessId, x.ProductVariantId, x.IdentifierType }).IsUnique().HasFilter("\"ProductVariantId\" IS NOT NULL");
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Cascade);
    }
}
