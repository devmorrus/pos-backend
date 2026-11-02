using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public static class IndustryProfileSeedIds
{
    public static readonly Guid General = Guid.Parse("ca100001-0000-4000-8000-000000000001");
    public static readonly Guid FoodBeverage = Guid.Parse("ca100002-0000-4000-8000-000000000002");
    public static readonly Guid Retail = Guid.Parse("ca100003-0000-4000-8000-000000000003");
    public static readonly Guid Grocery = Guid.Parse("ca100004-0000-4000-8000-000000000004");
    public static readonly Guid Fashion = Guid.Parse("ca100005-0000-4000-8000-000000000005");
    public static readonly Guid Services = Guid.Parse("ca100006-0000-4000-8000-000000000006");
}

internal static class CapabilitySeed
{
    private static readonly DateTime SeedDate = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

    public static readonly Capability[] All =
    [
        Create(1, CapabilityCodes.SalesPos, "POS Kasir", "Sales", CapabilityScopes.Outlet, true),
        Create(2, CapabilityCodes.SalesTransactions, "Riwayat Transaksi", "Sales", CapabilityScopes.Outlet, true),
        Create(3, CapabilityCodes.Customers, "Customer & Member", "Sales", CapabilityScopes.Business, true),
        Create(4, CapabilityCodes.CatalogProducts, "Produk & Kategori", "Catalog", CapabilityScopes.Business, true),
        Create(5, CapabilityCodes.CatalogVariants, "Varian Produk", "Catalog", CapabilityScopes.Business, false),
        Create(6, CapabilityCodes.CatalogModifiers, "Modifier Produk", "Catalog", CapabilityScopes.Business, false),
        Create(7, CapabilityCodes.CatalogRecipes, "Resep / BOM", "Catalog", CapabilityScopes.Business, false),
        Create(8, CapabilityCodes.CatalogBatchExpiry, "Batch & Kedaluwarsa", "Catalog", CapabilityScopes.Business, false),
        Create(9, CapabilityCodes.InventoryStock, "Manajemen Stok", "Inventory", CapabilityScopes.Outlet, true),
        Create(10, CapabilityCodes.InventoryBufferStock, "Buffer Stock", "Inventory", CapabilityScopes.Outlet, false),
        Create(11, CapabilityCodes.InventoryOpname, "Stock Opname", "Inventory", CapabilityScopes.Outlet, true),
        Create(12, CapabilityCodes.InventoryTransfer, "Transfer Stok", "Inventory", CapabilityScopes.Outlet, false),
        Create(13, CapabilityCodes.ProcurementPurchasing, "Supplier & Pembelian", "Procurement", CapabilityScopes.Outlet, false),
        Create(14, CapabilityCodes.ProcurementSupplierDebt, "Utang Supplier", "Procurement", CapabilityScopes.Business, false),
        Create(15, CapabilityCodes.ProcurementSupplierReturn, "Retur Supplier", "Procurement", CapabilityScopes.Outlet, false),
        Create(16, CapabilityCodes.Consignment, "Konsinyasi", "Procurement", CapabilityScopes.Outlet, false),
        Create(17, CapabilityCodes.ChannelsSettlement, "Channel Settlement", "Channels", CapabilityScopes.Outlet, false),
        Create(18, CapabilityCodes.ChannelsGoBiz, "Integrasi GoBiz", "Channels", CapabilityScopes.Outlet, false),
        Create(19, CapabilityCodes.PricingTaxService, "Pajak & Service Charge", "Pricing", CapabilityScopes.Outlet, false),
        Create(20, CapabilityCodes.PricingPromotions, "Promo Campaign", "Pricing", CapabilityScopes.Outlet, false),
        Create(21, CapabilityCodes.PricingVouchers, "Voucher", "Pricing", CapabilityScopes.Outlet, false),
        Create(22, CapabilityCodes.AccountingCore, "Akuntansi", "Accounting", CapabilityScopes.Business, false),
        Create(23, CapabilityCodes.AccountingCashflow, "Arus Kas Manual", "Accounting", CapabilityScopes.Business, false),
        Create(24, CapabilityCodes.AccountingIntegration, "Integrasi Akuntansi", "Accounting", CapabilityScopes.Business, false),
        Create(25, CapabilityCodes.ReportsOperational, "Laporan Operasional", "Reports", CapabilityScopes.Business, true),
        Create(26, CapabilityCodes.ReportsAccounting, "Laporan Akuntansi", "Reports", CapabilityScopes.Business, false),
        Create(27, CapabilityCodes.StorefrontPublic, "Public Storefront", "Channels", CapabilityScopes.Outlet, false),
    ];

    public static Guid IdFor(string code) => All.Single(x => x.Code == code).Id;

    private static Capability Create(int order, string code, string name, string module, string scope, bool enabled)
        => new()
        {
            Id = Guid.Parse($"ca200000-0000-4000-8000-{order:000000000000}"),
            Code = code,
            Name = name,
            Description = name,
            Module = module,
            Scope = scope,
            DefaultEnabled = enabled,
            IsActive = true,
            IsTenantConfigurable = true,
            SortOrder = order,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
        };
}

public class CapabilityConfiguration : IEntityTypeConfiguration<Capability>
{
    public void Configure(EntityTypeBuilder<Capability> builder)
    {
        builder.ToTable("capabilities", t => t.HasCheckConstraint(
            "CK_capabilities_Scope", "\"Scope\" IN ('Business','Outlet')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Module).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Scope).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.Module, x.SortOrder });
        builder.HasData(CapabilitySeed.All);
    }
}

public class IndustryProfileConfiguration : IEntityTypeConfiguration<IndustryProfile>
{
    public void Configure(EntityTypeBuilder<IndustryProfile> builder)
    {
        builder.ToTable("industry_profiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);

        var seedDate = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            Profile(IndustryProfileSeedIds.General, "general", "General", "Konfigurasi umum untuk bisnis lintas industri.", 1, seedDate),
            Profile(IndustryProfileSeedIds.FoodBeverage, "food-beverage", "Food & Beverage", "Kafe, restoran, bakery, dan usaha kuliner.", 2, seedDate),
            Profile(IndustryProfileSeedIds.Retail, "retail", "Retail", "Toko retail dan perdagangan umum.", 3, seedDate),
            Profile(IndustryProfileSeedIds.Grocery, "grocery", "Grocery", "Minimarket, sembako, dan produk dengan batch/expiry.", 4, seedDate),
            Profile(IndustryProfileSeedIds.Fashion, "fashion", "Fashion", "Butik, pakaian, dan produk dengan varian.", 5, seedDate),
            Profile(IndustryProfileSeedIds.Services, "services", "Services", "Salon, laundry, dan bisnis jasa.", 6, seedDate));
    }

    private static IndustryProfile Profile(Guid id, string code, string name, string description, int order, DateTime seedDate)
        => new()
        {
            Id = id,
            Code = code,
            Name = name,
            Description = description,
            IsActive = true,
            SortOrder = order,
            CreatedAt = seedDate,
            UpdatedAt = seedDate,
        };
}

public class IndustryProfileCapabilityConfiguration : IEntityTypeConfiguration<IndustryProfileCapability>
{
    public void Configure(EntityTypeBuilder<IndustryProfileCapability> builder)
    {
        builder.ToTable("industry_profile_capabilities");
        builder.HasKey(x => new { x.IndustryProfileId, x.CapabilityId });
        builder.HasOne(x => x.IndustryProfile).WithMany(x => x.Capabilities)
            .HasForeignKey(x => x.IndustryProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Capability).WithMany()
            .HasForeignKey(x => x.CapabilityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasData(BuildSeeds());
    }

    private static IEnumerable<IndustryProfileCapability> BuildSeeds()
    {
        var allCodes = CapabilitySeed.All.Select(x => x.Code).ToArray();
        var defaults = CapabilitySeed.All.Where(x => x.DefaultEnabled).Select(x => x.Code).ToHashSet();
        var profiles = new Dictionary<Guid, HashSet<string>>
        {
            [IndustryProfileSeedIds.General] = [.. defaults],
            [IndustryProfileSeedIds.FoodBeverage] = [.. defaults,
                CapabilityCodes.CatalogVariants, CapabilityCodes.CatalogModifiers, CapabilityCodes.CatalogRecipes,
                CapabilityCodes.InventoryBufferStock, CapabilityCodes.ProcurementPurchasing,
                CapabilityCodes.PricingTaxService, CapabilityCodes.PricingPromotions, CapabilityCodes.PricingVouchers,
                CapabilityCodes.ChannelsGoBiz, CapabilityCodes.StorefrontPublic, CapabilityCodes.AccountingCashflow],
            [IndustryProfileSeedIds.Retail] = [.. defaults,
                CapabilityCodes.CatalogVariants, CapabilityCodes.InventoryTransfer,
                CapabilityCodes.ProcurementPurchasing, CapabilityCodes.ProcurementSupplierDebt,
                CapabilityCodes.ProcurementSupplierReturn, CapabilityCodes.ChannelsSettlement,
                CapabilityCodes.PricingPromotions, CapabilityCodes.PricingVouchers, CapabilityCodes.StorefrontPublic],
            [IndustryProfileSeedIds.Grocery] = [.. defaults,
                CapabilityCodes.CatalogVariants, CapabilityCodes.CatalogBatchExpiry,
                CapabilityCodes.InventoryBufferStock, CapabilityCodes.InventoryTransfer,
                CapabilityCodes.ProcurementPurchasing, CapabilityCodes.ProcurementSupplierDebt,
                CapabilityCodes.ProcurementSupplierReturn, CapabilityCodes.PricingPromotions,
                CapabilityCodes.PricingVouchers, CapabilityCodes.StorefrontPublic],
            [IndustryProfileSeedIds.Fashion] = [.. defaults,
                CapabilityCodes.CatalogVariants, CapabilityCodes.InventoryTransfer,
                CapabilityCodes.ProcurementPurchasing, CapabilityCodes.ProcurementSupplierReturn,
                CapabilityCodes.PricingPromotions, CapabilityCodes.PricingVouchers, CapabilityCodes.StorefrontPublic],
            [IndustryProfileSeedIds.Services] = [CapabilityCodes.SalesPos, CapabilityCodes.SalesTransactions,
                CapabilityCodes.Customers, CapabilityCodes.CatalogProducts, CapabilityCodes.PricingTaxService,
                CapabilityCodes.PricingVouchers, CapabilityCodes.AccountingCashflow, CapabilityCodes.ReportsOperational],
        };

        return profiles.SelectMany(profile => allCodes.Select(code => new IndustryProfileCapability
        {
            IndustryProfileId = profile.Key,
            CapabilityId = CapabilitySeed.IdFor(code),
            IsEnabled = profile.Value.Contains(code),
        }));
    }
}

public class BusinessCapabilityOverrideConfiguration : IEntityTypeConfiguration<BusinessCapabilityOverride>
{
    public void Configure(EntityTypeBuilder<BusinessCapabilityOverride> builder)
    {
        builder.ToTable("business_capability_overrides");
        builder.HasKey(x => new { x.BusinessId, x.CapabilityId });
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasOne(x => x.Business).WithMany(x => x.CapabilityOverrides)
            .HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Capability).WithMany()
            .HasForeignKey(x => x.CapabilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany()
            .HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UpdatedByUserId);
    }
}

public class OutletCapabilityOverrideConfiguration : IEntityTypeConfiguration<OutletCapabilityOverride>
{
    public void Configure(EntityTypeBuilder<OutletCapabilityOverride> builder)
    {
        builder.ToTable("outlet_capability_overrides");
        builder.HasKey(x => new { x.BusinessId, x.OutletId, x.CapabilityId });
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasOne(x => x.Business).WithMany()
            .HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Outlet).WithMany(x => x.CapabilityOverrides)
            .HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Capability).WithMany()
            .HasForeignKey(x => x.CapabilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany()
            .HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.OutletId);
        builder.HasIndex(x => x.UpdatedByUserId);
    }
}

public class CapabilityChangeLogConfiguration : IEntityTypeConfiguration<CapabilityChangeLog>
{
    public void Configure(EntityTypeBuilder<CapabilityChangeLog> builder)
    {
        builder.ToTable("capability_change_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ChangeType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasOne(x => x.Business).WithMany()
            .HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Outlet).WithMany()
            .HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Capability).WithMany()
            .HasForeignKey(x => x.CapabilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ChangedByUser).WithMany()
            .HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IndustryProfile>().WithMany().HasForeignKey(x => x.OldProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IndustryProfile>().WithMany().HasForeignKey(x => x.NewProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BusinessId, x.CreatedAt });
        builder.HasIndex(x => new { x.BusinessId, x.CapabilityId, x.CreatedAt });
        builder.HasIndex(x => new { x.BusinessId, x.OutletId, x.CreatedAt });
    }
}
