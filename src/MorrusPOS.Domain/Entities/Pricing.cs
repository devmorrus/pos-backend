using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class PricingDiscountType
{
    public const string Fixed = "fixed";
    public const string Percentage = "percentage";
}

public static class PromoScopeType
{
    public const string Transaction = "transaction";
    public const string Product = "product";
    public const string Category = "category";
}

public class TaxRule : AuditableEntity
{
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public string Name { get; set; } = default!;
    public decimal Rate { get; set; }
    public bool IsActive { get; set; } = true;
    public bool AppliesBeforeServiceCharge { get; set; } = true;
}

public class ServiceChargeRule : AuditableEntity
{
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public string Name { get; set; } = default!;
    public decimal Rate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PromoCampaign : AuditableEntity
{
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public string? Code { get; set; }
    public string Name { get; set; } = default!;
    public string DiscountType { get; set; } = PricingDiscountType.Fixed;
    public decimal DiscountValue { get; set; }
    public string ScopeType { get; set; } = PromoScopeType.Transaction;
    public decimal MinimumSpend { get; set; }
    public decimal? MaximumDiscountAmount { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<PromoCampaignTarget> Targets { get; set; } = new List<PromoCampaignTarget>();
}

public class PromoCampaignTarget : BaseEntity
{
    public Guid PromoCampaignId { get; set; }
    public PromoCampaign PromoCampaign { get; set; } = default!;

    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
}

public class Voucher : AuditableEntity
{
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string DiscountType { get; set; } = PricingDiscountType.Fixed;
    public decimal DiscountValue { get; set; }
    public decimal MinimumSpend { get; set; }
    public decimal? MaximumDiscountAmount { get; set; }
    public int UsageLimitTotal { get; set; } = 1;
    public int UsageLimitPerCode { get; set; } = 1;
    public int UsedCount { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<VoucherRedemption> Redemptions { get; set; } = new List<VoucherRedemption>();
}

public class VoucherRedemption : BaseEntity
{
    public Guid VoucherId { get; set; }
    public Voucher Voucher { get; set; } = default!;

    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = default!;

    public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;
    public decimal RedeemedAmount { get; set; }
}
