using FluentValidation;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Application.Features.Pricing.Validators;

public class CreateTaxRuleRequestValidator : AbstractValidator<CreateTaxRuleRequest>
{
    public CreateTaxRuleRequestValidator()
    {
        RuleFor(x => x.OutletId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
    }
}

public class UpdateTaxRuleRequestValidator : AbstractValidator<UpdateTaxRuleRequest>
{
    public UpdateTaxRuleRequestValidator()
    {
        RuleFor(x => x.OutletId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
    }
}

public class CreateServiceChargeRuleRequestValidator : AbstractValidator<CreateServiceChargeRuleRequest>
{
    public CreateServiceChargeRuleRequestValidator()
    {
        RuleFor(x => x.OutletId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
    }
}

public class UpdateServiceChargeRuleRequestValidator : AbstractValidator<UpdateServiceChargeRuleRequest>
{
    public UpdateServiceChargeRuleRequestValidator()
    {
        RuleFor(x => x.OutletId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
    }
}

public class PromoCampaignTargetRequestValidator : AbstractValidator<PromoCampaignTargetRequest>
{
    public PromoCampaignTargetRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.ProductId.HasValue || x.CategoryId.HasValue)
            .WithMessage("Target promo harus memiliki productId atau categoryId.");
    }
}

public class CreatePromoCampaignRequestValidator : AbstractValidator<CreatePromoCampaignRequest>
{
    public CreatePromoCampaignRequestValidator()
    {
        RuleFor(x => x.OutletId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DiscountType)
            .Must(value => value is PricingDiscountType.Fixed or PricingDiscountType.Percentage)
            .WithMessage("DiscountType promo harus fixed atau percentage.");
        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.ScopeType)
            .Must(value => value is PromoScopeType.Transaction or PromoScopeType.Product or PromoScopeType.Category)
            .WithMessage("ScopeType promo harus transaction, product, atau category.");
        RuleFor(x => x.MinimumSpend).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaximumDiscountAmount).GreaterThan(0).When(x => x.MaximumDiscountAmount != null);
        RuleFor(x => x.EndAt).GreaterThan(x => x.StartAt);
        RuleForEach(x => x.Targets).SetValidator(new PromoCampaignTargetRequestValidator());
        RuleFor(x => x)
            .Must(model => model.ScopeType == PromoScopeType.Transaction || model.Targets.Any())
            .WithMessage("Promo product/category wajib memiliki target.");
    }
}

public class UpdatePromoCampaignRequestValidator : AbstractValidator<UpdatePromoCampaignRequest>
{
    public UpdatePromoCampaignRequestValidator()
    {
        RuleFor(x => x.OutletId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DiscountType)
            .Must(value => value is PricingDiscountType.Fixed or PricingDiscountType.Percentage)
            .WithMessage("DiscountType promo harus fixed atau percentage.");
        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.ScopeType)
            .Must(value => value is PromoScopeType.Transaction or PromoScopeType.Product or PromoScopeType.Category)
            .WithMessage("ScopeType promo harus transaction, product, atau category.");
        RuleFor(x => x.MinimumSpend).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaximumDiscountAmount).GreaterThan(0).When(x => x.MaximumDiscountAmount != null);
        RuleFor(x => x.EndAt).GreaterThan(x => x.StartAt);
        RuleForEach(x => x.Targets).SetValidator(new PromoCampaignTargetRequestValidator());
        RuleFor(x => x)
            .Must(model => model.ScopeType == PromoScopeType.Transaction || model.Targets.Any())
            .WithMessage("Promo product/category wajib memiliki target.");
    }
}

public class CreateVoucherRequestValidator : AbstractValidator<CreateVoucherRequest>
{
    public CreateVoucherRequestValidator()
    {
        RuleFor(x => x.OutletId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DiscountType)
            .Must(value => value is PricingDiscountType.Fixed or PricingDiscountType.Percentage)
            .WithMessage("DiscountType voucher harus fixed atau percentage.");
        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.MinimumSpend).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaximumDiscountAmount).GreaterThan(0).When(x => x.MaximumDiscountAmount != null);
        RuleFor(x => x.UsageLimitTotal).GreaterThan(0);
        RuleFor(x => x.UsageLimitPerCode).GreaterThan(0);
        RuleFor(x => x.EndAt).GreaterThan(x => x.StartAt);
    }
}

public class UpdateVoucherRequestValidator : AbstractValidator<UpdateVoucherRequest>
{
    public UpdateVoucherRequestValidator()
    {
        RuleFor(x => x.OutletId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DiscountType)
            .Must(value => value is PricingDiscountType.Fixed or PricingDiscountType.Percentage)
            .WithMessage("DiscountType voucher harus fixed atau percentage.");
        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.MinimumSpend).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaximumDiscountAmount).GreaterThan(0).When(x => x.MaximumDiscountAmount != null);
        RuleFor(x => x.UsageLimitTotal).GreaterThan(0);
        RuleFor(x => x.UsageLimitPerCode).GreaterThan(0);
        RuleFor(x => x.EndAt).GreaterThan(x => x.StartAt);
    }
}
