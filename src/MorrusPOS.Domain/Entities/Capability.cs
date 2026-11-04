using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class CapabilityScopes
{
    public const string Business = "Business";
    public const string Outlet = "Outlet";
}

public static class CapabilityCodes
{
    public const string SalesPos = "sales.pos";
    public const string SalesTransactions = "sales.transactions";
    public const string Customers = "customers";
    public const string CatalogProducts = "catalog.products";
    public const string CatalogVariants = "catalog.variants";
    public const string CatalogModifiers = "catalog.modifiers";
    public const string CatalogRecipes = "catalog.recipes";
    public const string CatalogBatchExpiry = "catalog.batch_expiry";
    public const string CatalogSerialTracking = "catalog.serial_tracking";
    public const string InventoryStock = "inventory.stock";
    public const string InventoryBufferStock = "inventory.buffer_stock";
    public const string InventoryOpname = "inventory.opname";
    public const string InventoryTransfer = "inventory.transfer";
    public const string ProcurementPurchasing = "procurement.purchasing";
    public const string ProcurementSupplierDebt = "procurement.supplier_debt";
    public const string ProcurementSupplierReturn = "procurement.supplier_return";
    public const string Consignment = "consignment";
    public const string ChannelsSettlement = "channels.settlement";
    public const string ChannelsGoBiz = "channels.gobiz";
    public const string PricingTaxService = "pricing.tax_service";
    public const string PricingPromotions = "pricing.promotions";
    public const string PricingVouchers = "pricing.vouchers";
    public const string AccountingCore = "accounting.core";
    public const string AccountingCashflow = "accounting.cashflow";
    public const string AccountingIntegration = "accounting.integration";
    public const string ReportsOperational = "reports.operational";
    public const string ReportsAccounting = "reports.accounting";
    public const string StorefrontPublic = "storefront.public";
}

public class Capability : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Module { get; set; } = default!;
    public string Scope { get; set; } = CapabilityScopes.Business;
    public bool DefaultEnabled { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsTenantConfigurable { get; set; } = true;
    public int SortOrder { get; set; }
}

public class IndustryProfile : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<IndustryProfileCapability> Capabilities { get; set; } = new List<IndustryProfileCapability>();
    public ICollection<Business> Businesses { get; set; } = new List<Business>();
}

public class IndustryProfileCapability
{
    public Guid IndustryProfileId { get; set; }
    public IndustryProfile IndustryProfile { get; set; } = default!;
    public Guid CapabilityId { get; set; }
    public Capability Capability { get; set; } = default!;
    public bool IsEnabled { get; set; }
}

public class BusinessCapabilityOverride
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid CapabilityId { get; set; }
    public Capability Capability { get; set; } = default!;
    public bool IsEnabled { get; set; }
    public string? Reason { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public User UpdatedByUser { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class OutletCapabilityOverride
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;
    public Guid CapabilityId { get; set; }
    public Capability Capability { get; set; } = default!;
    public bool IsEnabled { get; set; }
    public string? Reason { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public User UpdatedByUser { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class CapabilityChangeLog : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }
    public Guid? CapabilityId { get; set; }
    public Capability? Capability { get; set; }
    public string ChangeType { get; set; } = default!;
    public bool? OldValue { get; set; }
    public bool? NewValue { get; set; }
    public Guid? OldProfileId { get; set; }
    public Guid? NewProfileId { get; set; }
    public string? Reason { get; set; }
    public Guid ChangedByUserId { get; set; }
    public User ChangedByUser { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
