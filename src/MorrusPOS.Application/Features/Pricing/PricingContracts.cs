namespace MorrusPOS.Application.Features.Pricing;

public record TaxRuleDto(
    Guid Id,
    Guid OutletId,
    string OutletName,
    string Name,
    decimal Rate,
    bool IsActive,
    bool AppliesBeforeServiceCharge,
    DateTime UpdatedAt
);

public record ServiceChargeRuleDto(
    Guid Id,
    Guid OutletId,
    string OutletName,
    string Name,
    decimal Rate,
    bool IsActive,
    DateTime UpdatedAt
);

public record PromoCampaignDto(
    Guid Id,
    Guid OutletId,
    string OutletName,
    string? Code,
    string Name,
    string DiscountType,
    decimal DiscountValue,
    string ScopeType,
    decimal MinimumSpend,
    decimal? MaximumDiscountAmount,
    DateTime StartAt,
    DateTime EndAt,
    bool IsActive,
    IReadOnlyList<Guid> ProductIds,
    IReadOnlyList<Guid> CategoryIds
);

public record VoucherDto(
    Guid Id,
    Guid OutletId,
    string OutletName,
    string Code,
    string Name,
    string DiscountType,
    decimal DiscountValue,
    decimal MinimumSpend,
    decimal? MaximumDiscountAmount,
    int UsageLimitTotal,
    int UsageLimitPerCode,
    int UsedCount,
    DateTime StartAt,
    DateTime EndAt,
    bool IsActive
);

public record CreateTaxRuleRequest(
    Guid OutletId,
    string Name,
    decimal Rate,
    bool IsActive,
    bool AppliesBeforeServiceCharge
);

public record UpdateTaxRuleRequest(
    Guid OutletId,
    string Name,
    decimal Rate,
    bool IsActive,
    bool AppliesBeforeServiceCharge
);

public record CreateServiceChargeRuleRequest(
    Guid OutletId,
    string Name,
    decimal Rate,
    bool IsActive
);

public record UpdateServiceChargeRuleRequest(
    Guid OutletId,
    string Name,
    decimal Rate,
    bool IsActive
);

public record PromoCampaignTargetRequest(
    Guid? ProductId,
    Guid? CategoryId
);

public record CreatePromoCampaignRequest(
    Guid OutletId,
    string? Code,
    string Name,
    string DiscountType,
    decimal DiscountValue,
    string ScopeType,
    decimal MinimumSpend,
    decimal? MaximumDiscountAmount,
    DateTime StartAt,
    DateTime EndAt,
    bool IsActive,
    List<PromoCampaignTargetRequest> Targets
);

public record UpdatePromoCampaignRequest(
    Guid OutletId,
    string? Code,
    string Name,
    string DiscountType,
    decimal DiscountValue,
    string ScopeType,
    decimal MinimumSpend,
    decimal? MaximumDiscountAmount,
    DateTime StartAt,
    DateTime EndAt,
    bool IsActive,
    List<PromoCampaignTargetRequest> Targets
);

public record CreateVoucherRequest(
    Guid OutletId,
    string Code,
    string Name,
    string DiscountType,
    decimal DiscountValue,
    decimal MinimumSpend,
    decimal? MaximumDiscountAmount,
    int UsageLimitTotal,
    int UsageLimitPerCode,
    DateTime StartAt,
    DateTime EndAt,
    bool IsActive
);

public record UpdateVoucherRequest(
    Guid OutletId,
    string Code,
    string Name,
    string DiscountType,
    decimal DiscountValue,
    decimal MinimumSpend,
    decimal? MaximumDiscountAmount,
    int UsageLimitTotal,
    int UsageLimitPerCode,
    DateTime StartAt,
    DateTime EndAt,
    bool IsActive
);

public interface IPricingAdminService
{
    Task<IReadOnlyList<TaxRuleDto>> GetTaxRulesAsync(Guid? outletId, CancellationToken ct = default);
    Task<TaxRuleDto> CreateTaxRuleAsync(CreateTaxRuleRequest request, CancellationToken ct = default);
    Task<TaxRuleDto> UpdateTaxRuleAsync(Guid id, UpdateTaxRuleRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceChargeRuleDto>> GetServiceChargeRulesAsync(Guid? outletId, CancellationToken ct = default);
    Task<ServiceChargeRuleDto> CreateServiceChargeRuleAsync(CreateServiceChargeRuleRequest request, CancellationToken ct = default);
    Task<ServiceChargeRuleDto> UpdateServiceChargeRuleAsync(Guid id, UpdateServiceChargeRuleRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<PromoCampaignDto>> GetPromoCampaignsAsync(Guid? outletId, CancellationToken ct = default);
    Task<PromoCampaignDto> CreatePromoCampaignAsync(CreatePromoCampaignRequest request, CancellationToken ct = default);
    Task<PromoCampaignDto> UpdatePromoCampaignAsync(Guid id, UpdatePromoCampaignRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<VoucherDto>> GetVouchersAsync(Guid? outletId, CancellationToken ct = default);
    Task<VoucherDto> CreateVoucherAsync(CreateVoucherRequest request, CancellationToken ct = default);
    Task<VoucherDto> UpdateVoucherAsync(Guid id, UpdateVoucherRequest request, CancellationToken ct = default);
    Task<VoucherDto> SetVoucherActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
}
