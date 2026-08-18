using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Pricing;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public interface IPricingService
{
    Task<PricingBreakdownDto> CalculateAsync(PricingPreviewRequest request, CancellationToken ct = default);
}

public class PricingService : IPricingService
{
    private readonly AppDbContext _dbContext;

    public PricingService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PricingBreakdownDto> CalculateAsync(PricingPreviewRequest request, CancellationToken ct = default)
    {
        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Keranjang tidak boleh kosong.");
        }

        var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .Include(p => p.Category)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        if (products.Count != productIds.Count)
        {
            throw new InvalidOperationException("Ada produk checkout yang tidak ditemukan.");
        }

        var now = DateTime.UtcNow;
        var taxRule = await _dbContext.TaxRules
            .AsNoTracking()
            .Where(rule => rule.OutletId == request.OutletId && rule.IsActive)
            .OrderByDescending(rule => rule.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        var serviceChargeRule = await _dbContext.ServiceChargeRules
            .AsNoTracking()
            .Where(rule => rule.OutletId == request.OutletId && rule.IsActive)
            .OrderByDescending(rule => rule.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        var promoCandidates = request.Channel == TransactionChannel.Pos
            ? await _dbContext.PromoCampaigns
                .AsNoTracking()
                .Include(p => p.Targets)
                .Where(p => p.OutletId == request.OutletId &&
                            p.IsActive &&
                            p.StartAt <= now &&
                            p.EndAt >= now &&
                            (request.SelectedPromoCode == null || p.Code == request.SelectedPromoCode))
                .ToListAsync(ct)
            : new List<PromoCampaign>();

        var voucher = string.IsNullOrWhiteSpace(request.VoucherCode)
            ? null
            : await _dbContext.Vouchers
                .AsNoTracking()
                .FirstOrDefaultAsync(v =>
                    v.OutletId == request.OutletId &&
                    v.Code == request.VoucherCode!.Trim() &&
                    v.IsActive &&
                    v.StartAt <= now &&
                    v.EndAt >= now,
                    ct);

        var lines = request.Items.Select(item =>
        {
            var product = products[item.ProductId];
            var subtotal = RoundMoney(item.UnitPrice * item.Qty);
            var manualDiscount = RoundMoney(item.DiscountAmount);
            if (manualDiscount > subtotal)
            {
                throw new InvalidOperationException($"Diskon untuk produk {product.Name} melebihi subtotal item.");
            }

            return new PricingLineState
            {
                Product = product,
                ProductVariantId = item.ProductVariantId,
                Qty = item.Qty,
                UnitPrice = item.UnitPrice,
                Subtotal = subtotal,
                ManualDiscount = manualDiscount
            };
        }).ToList();

        var subtotalTotal = RoundMoney(lines.Sum(line => line.Subtotal));
        var manualDiscountTotal = RoundMoney(lines.Sum(line => line.ManualDiscount));
        var discountedBase = RoundMoney(lines.Sum(line => line.NetAfterManual));

        var bestPromo = FindBestPromo(promoCandidates, lines, discountedBase);
        var voucherCandidate = FindVoucherCandidate(voucher, discountedBase);

        if (bestPromo != null && voucherCandidate != null)
        {
            if (voucherCandidate.DiscountAmount >= bestPromo.DiscountAmount)
            {
                bestPromo = null;
            }
            else
            {
                voucherCandidate = null;
            }
        }

        if (bestPromo != null)
        {
            AllocateDiscount(lines, bestPromo.EligibleLines, bestPromo.DiscountAmount, DiscountSource.Promo);
        }

        if (voucherCandidate != null)
        {
            AllocateDiscount(lines, lines.Where(line => line.NetAfterManualAndPromo > 0).ToList(), voucherCandidate.DiscountAmount, DiscountSource.Voucher);
        }

        var serviceChargeBase = RoundMoney(lines.Where(line => IsServiceChargeable(line.Product, serviceChargeRule)).Sum(line => line.NetAfterDiscounts));
        var serviceChargeTotal = serviceChargeRule == null
            ? 0m
            : RoundMoney(serviceChargeBase * (serviceChargeRule.Rate / 100m));

        AllocateCharge(lines.Where(line => IsServiceChargeable(line.Product, serviceChargeRule)).ToList(), serviceChargeTotal, ChargeSource.ServiceCharge);

        var taxableBase = RoundMoney(lines.Where(line => IsTaxable(line.Product, taxRule)).Sum(line =>
            line.NetAfterDiscounts + (taxRule?.AppliesBeforeServiceCharge == false ? line.ServiceCharge : 0m)));
        var taxTotal = taxRule == null
            ? 0m
            : RoundMoney(taxableBase * (taxRule.Rate / 100m));

        AllocateCharge(lines.Where(line => IsTaxable(line.Product, taxRule)).ToList(), taxTotal, ChargeSource.Tax);

        var promoDiscountTotal = RoundMoney(lines.Sum(line => line.PromoDiscount));
        var voucherDiscountTotal = RoundMoney(lines.Sum(line => line.VoucherDiscount));
        var grandTotal = RoundMoney(lines.Sum(line => line.LineGrandTotal));

        if (grandTotal < 0)
        {
            throw new InvalidOperationException("Grand total transaksi tidak boleh negatif.");
        }

        return new PricingBreakdownDto(
            subtotalTotal,
            manualDiscountTotal,
            promoDiscountTotal,
            voucherDiscountTotal,
            RoundMoney(lines.Sum(line => line.ServiceCharge)),
            RoundMoney(lines.Sum(line => line.Tax)),
            grandTotal,
            voucherCandidate == null || voucher == null
                ? null
                : new AppliedVoucherDto(voucher.Id, voucher.Code, voucher.Name, voucherDiscountTotal),
            bestPromo == null
                ? null
                : new AppliedPromoDto(bestPromo.Promo.Id, bestPromo.Promo.Code, bestPromo.Promo.Name, promoDiscountTotal),
            lines.Select(line => new PricingLineBreakdownDto(
                line.Product.Id,
                line.Product.Name,
                line.Qty,
                line.Subtotal,
                line.ManualDiscount,
                line.PromoDiscount,
                line.VoucherDiscount,
                line.ServiceCharge,
                line.Tax,
                line.LineGrandTotal
            )
            {
                ProductVariantId = line.ProductVariantId
            }).ToList()
        );
    }

    private PromoCandidate? FindBestPromo(IReadOnlyCollection<PromoCampaign> promos, IReadOnlyList<PricingLineState> lines, decimal discountedBase)
    {
        PromoCandidate? best = null;

        foreach (var promo in promos)
        {
            var eligibleLines = GetEligiblePromoLines(promo, lines);
            var eligibleBase = RoundMoney(eligibleLines.Sum(line => line.NetAfterManual));
            if (eligibleBase <= 0 || eligibleBase < promo.MinimumSpend)
            {
                continue;
            }

            var discountAmount = CalculateDiscountAmount(promo.DiscountType, promo.DiscountValue, eligibleBase, promo.MaximumDiscountAmount);
            if (discountAmount <= 0)
            {
                continue;
            }

            var candidate = new PromoCandidate(promo, eligibleLines, discountAmount);
            if (best == null || candidate.DiscountAmount > best.DiscountAmount)
            {
                best = candidate;
            }
        }

        return best;
    }

    private VoucherCandidate? FindVoucherCandidate(Voucher? voucher, decimal discountedBase)
    {
        if (voucher == null)
        {
            return null;
        }

        if (discountedBase < voucher.MinimumSpend)
        {
            throw new InvalidOperationException("Voucher belum memenuhi minimum belanja.");
        }

        if (voucher.UsedCount >= voucher.UsageLimitTotal)
        {
            throw new InvalidOperationException("Voucher sudah mencapai batas penggunaan.");
        }

        var discountAmount = CalculateDiscountAmount(voucher.DiscountType, voucher.DiscountValue, discountedBase, voucher.MaximumDiscountAmount);
        if (discountAmount <= 0)
        {
            return null;
        }

        return new VoucherCandidate(voucher, discountAmount);
    }

    private static List<PricingLineState> GetEligiblePromoLines(PromoCampaign promo, IReadOnlyList<PricingLineState> lines)
    {
        if (promo.ScopeType == PromoScopeType.Transaction)
        {
            return lines.Where(line => line.NetAfterManual > 0).ToList();
        }

        var productTargets = promo.Targets.Where(target => target.ProductId.HasValue).Select(target => target.ProductId!.Value).ToHashSet();
        var categoryTargets = promo.Targets.Where(target => target.CategoryId.HasValue).Select(target => target.CategoryId!.Value).ToHashSet();

        return lines.Where(line =>
                line.NetAfterManual > 0 &&
                (promo.ScopeType == PromoScopeType.Product && productTargets.Contains(line.Product.Id) ||
                 promo.ScopeType == PromoScopeType.Category && categoryTargets.Contains(line.Product.CategoryId)))
            .ToList();
    }

    private static decimal CalculateDiscountAmount(string discountType, decimal discountValue, decimal eligibleBase, decimal? maxDiscount)
    {
        var rawAmount = discountType == PricingDiscountType.Percentage
            ? eligibleBase * (discountValue / 100m)
            : discountValue;
        var bounded = Math.Min(eligibleBase, rawAmount);
        if (maxDiscount.HasValue)
        {
            bounded = Math.Min(bounded, maxDiscount.Value);
        }

        return RoundMoney(bounded);
    }

    private static void AllocateDiscount(IReadOnlyList<PricingLineState> lines, IReadOnlyList<PricingLineState> eligibleLines, decimal total, DiscountSource source)
    {
        if (total <= 0 || eligibleLines.Count == 0)
        {
            return;
        }

        var baseTotal = eligibleLines.Sum(line => line.NetAfterManualAndPromoForAllocation(source));
        if (baseTotal <= 0)
        {
            return;
        }

        var remaining = total;
        for (var i = 0; i < eligibleLines.Count; i++)
        {
            var line = eligibleLines[i];
            var lineAmount = i == eligibleLines.Count - 1
                ? remaining
                : RoundMoney(total * (line.NetAfterManualAndPromoForAllocation(source) / baseTotal));
            var bounded = Math.Min(line.NetAfterManualAndPromoForAllocation(source), lineAmount);
            ApplyDiscount(line, bounded, source);
            remaining = RoundMoney(remaining - bounded);
        }
    }

    private static void AllocateCharge(IReadOnlyList<PricingLineState> lines, decimal total, ChargeSource source)
    {
        if (total <= 0 || lines.Count == 0)
        {
            return;
        }

        var baseTotal = lines.Sum(line => line.NetAfterDiscounts);
        if (baseTotal <= 0)
        {
            return;
        }

        var remaining = total;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var baseAmount = line.NetAfterDiscounts;
            var lineAmount = i == lines.Count - 1
                ? remaining
                : RoundMoney(total * (baseAmount / baseTotal));
            ApplyCharge(line, lineAmount, source);
            remaining = RoundMoney(remaining - lineAmount);
        }
    }

    private static void ApplyDiscount(PricingLineState line, decimal amount, DiscountSource source)
    {
        if (source == DiscountSource.Promo)
        {
            line.PromoDiscount = RoundMoney(line.PromoDiscount + amount);
            return;
        }

        line.VoucherDiscount = RoundMoney(line.VoucherDiscount + amount);
    }

    private static void ApplyCharge(PricingLineState line, decimal amount, ChargeSource source)
    {
        if (source == ChargeSource.ServiceCharge)
        {
            line.ServiceCharge = RoundMoney(line.ServiceCharge + amount);
            return;
        }

        line.Tax = RoundMoney(line.Tax + amount);
    }

    private static bool IsTaxable(Product product, TaxRule? taxRule)
    {
        if (taxRule == null)
        {
            return false;
        }

        return product.IsTaxable ?? true;
    }

    private static bool IsServiceChargeable(Product product, ServiceChargeRule? serviceChargeRule)
    {
        if (serviceChargeRule == null)
        {
            return false;
        }

        return product.IsServiceChargeable ?? true;
    }

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed class PricingLineState
    {
        public required Product Product { get; init; }
        public Guid? ProductVariantId { get; init; }
        public decimal Qty { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal Subtotal { get; init; }
        public decimal ManualDiscount { get; init; }
        public decimal PromoDiscount { get; set; }
        public decimal VoucherDiscount { get; set; }
        public decimal ServiceCharge { get; set; }
        public decimal Tax { get; set; }

        public decimal NetAfterManual => RoundMoney(Subtotal - ManualDiscount);
        public decimal NetAfterManualAndPromo => RoundMoney(NetAfterManual - PromoDiscount);
        public decimal NetAfterDiscounts => RoundMoney(NetAfterManual - PromoDiscount - VoucherDiscount);
        public decimal LineGrandTotal => RoundMoney(NetAfterDiscounts + ServiceCharge + Tax);

        public decimal NetAfterManualAndPromoForAllocation(DiscountSource source)
            => source == DiscountSource.Promo ? NetAfterManual : NetAfterManualAndPromo;
    }

    private sealed record PromoCandidate(PromoCampaign Promo, IReadOnlyList<PricingLineState> EligibleLines, decimal DiscountAmount);
    private sealed record VoucherCandidate(Voucher Voucher, decimal DiscountAmount);
    private enum DiscountSource { Promo, Voucher }
    private enum ChargeSource { ServiceCharge, Tax }
}

public class PricingAdminService : IPricingAdminService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public PricingAdminService(AppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<TaxRuleDto>> GetTaxRulesAsync(Guid? outletId, CancellationToken ct = default)
    {
        var query = _dbContext.TaxRules.Include(x => x.Outlet).AsNoTracking().AsQueryable();
        if (outletId.HasValue)
        {
            await EnsureOutletAccessibleAsync(outletId.Value, ct);
            query = query.Where(x => x.OutletId == outletId.Value);
        }

        var entities = await query
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(ct);
        return entities.Select(entity => new TaxRuleDto(entity.Id, entity.OutletId, entity.Outlet.Name, entity.Name, entity.Rate, entity.IsActive, entity.AppliesBeforeServiceCharge, entity.UpdatedAt)).ToList();
    }

    public async Task<TaxRuleDto> CreateTaxRuleAsync(CreateTaxRuleRequest request, CancellationToken ct = default)
    {
        await EnsureOutletAccessibleAsync(request.OutletId, ct);
        var entity = new TaxRule
        {
            Id = Guid.NewGuid(),
            OutletId = request.OutletId,
            Name = request.Name.Trim(),
            Rate = request.Rate,
            IsActive = request.IsActive,
            AppliesBeforeServiceCharge = request.AppliesBeforeServiceCharge,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.TaxRules.Add(entity);
        await _dbContext.SaveChangesAsync(ct);
        var created = await _dbContext.TaxRules.Include(x => x.Outlet).AsNoTracking().FirstAsync(x => x.Id == entity.Id, ct);
        return new TaxRuleDto(created.Id, created.OutletId, created.Outlet.Name, created.Name, created.Rate, created.IsActive, created.AppliesBeforeServiceCharge, created.UpdatedAt);
    }

    public async Task<TaxRuleDto> UpdateTaxRuleAsync(Guid id, UpdateTaxRuleRequest request, CancellationToken ct = default)
    {
        var entity = await _dbContext.TaxRules.Include(x => x.Outlet).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Tax rule tidak ditemukan.");
        await EnsureOutletAccessibleAsync(entity.OutletId, ct);
        entity.OutletId = request.OutletId;
        entity.Name = request.Name.Trim();
        entity.Rate = request.Rate;
        entity.IsActive = request.IsActive;
        entity.AppliesBeforeServiceCharge = request.AppliesBeforeServiceCharge;
        entity.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return new TaxRuleDto(entity.Id, entity.OutletId, entity.Outlet.Name, entity.Name, entity.Rate, entity.IsActive, entity.AppliesBeforeServiceCharge, entity.UpdatedAt);
    }

    public async Task<IReadOnlyList<ServiceChargeRuleDto>> GetServiceChargeRulesAsync(Guid? outletId, CancellationToken ct = default)
    {
        var query = _dbContext.ServiceChargeRules.Include(x => x.Outlet).AsNoTracking().AsQueryable();
        if (outletId.HasValue)
        {
            await EnsureOutletAccessibleAsync(outletId.Value, ct);
            query = query.Where(x => x.OutletId == outletId.Value);
        }

        var entities = await query
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(ct);
        return entities.Select(entity => new ServiceChargeRuleDto(entity.Id, entity.OutletId, entity.Outlet.Name, entity.Name, entity.Rate, entity.IsActive, entity.UpdatedAt)).ToList();
    }

    public async Task<ServiceChargeRuleDto> CreateServiceChargeRuleAsync(CreateServiceChargeRuleRequest request, CancellationToken ct = default)
    {
        await EnsureOutletAccessibleAsync(request.OutletId, ct);
        var entity = new ServiceChargeRule
        {
            Id = Guid.NewGuid(),
            OutletId = request.OutletId,
            Name = request.Name.Trim(),
            Rate = request.Rate,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.ServiceChargeRules.Add(entity);
        await _dbContext.SaveChangesAsync(ct);
        var created = await _dbContext.ServiceChargeRules.Include(x => x.Outlet).AsNoTracking().FirstAsync(x => x.Id == entity.Id, ct);
        return new ServiceChargeRuleDto(created.Id, created.OutletId, created.Outlet.Name, created.Name, created.Rate, created.IsActive, created.UpdatedAt);
    }

    public async Task<ServiceChargeRuleDto> UpdateServiceChargeRuleAsync(Guid id, UpdateServiceChargeRuleRequest request, CancellationToken ct = default)
    {
        var entity = await _dbContext.ServiceChargeRules.Include(x => x.Outlet).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Service charge rule tidak ditemukan.");
        await EnsureOutletAccessibleAsync(entity.OutletId, ct);
        entity.OutletId = request.OutletId;
        entity.Name = request.Name.Trim();
        entity.Rate = request.Rate;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return new ServiceChargeRuleDto(entity.Id, entity.OutletId, entity.Outlet.Name, entity.Name, entity.Rate, entity.IsActive, entity.UpdatedAt);
    }

    public async Task<IReadOnlyList<PromoCampaignDto>> GetPromoCampaignsAsync(Guid? outletId, CancellationToken ct = default)
    {
        var query = _dbContext.PromoCampaigns.Include(x => x.Outlet).Include(x => x.Targets).AsNoTracking().AsQueryable();
        if (outletId.HasValue)
        {
            await EnsureOutletAccessibleAsync(outletId.Value, ct);
            query = query.Where(x => x.OutletId == outletId.Value);
        }

        var promos = await query.OrderByDescending(x => x.UpdatedAt).ToListAsync(ct);
        return promos.Select(MapPromoCampaignDto).ToList();
    }

    public async Task<PromoCampaignDto> CreatePromoCampaignAsync(CreatePromoCampaignRequest request, CancellationToken ct = default)
    {
        await EnsureOutletAccessibleAsync(request.OutletId, ct);
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var exists = await _dbContext.PromoCampaigns.AnyAsync(x => x.OutletId == request.OutletId && x.Code == request.Code.Trim(), ct);
            if (exists)
            {
                throw new InvalidOperationException("Kode promo sudah digunakan pada outlet ini.");
            }
        }

        var entity = new PromoCampaign
        {
            Id = Guid.NewGuid(),
            OutletId = request.OutletId,
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
            Name = request.Name.Trim(),
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            ScopeType = request.ScopeType,
            MinimumSpend = request.MinimumSpend,
            MaximumDiscountAmount = request.MaximumDiscountAmount,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Targets = request.Targets.Select(target => new PromoCampaignTarget
            {
                Id = Guid.NewGuid(),
                ProductId = target.ProductId,
                CategoryId = target.CategoryId
            }).ToList()
        };
        _dbContext.PromoCampaigns.Add(entity);
        await _dbContext.SaveChangesAsync(ct);
        return await GetPromoByIdAsync(entity.Id, ct);
    }

    public async Task<PromoCampaignDto> UpdatePromoCampaignAsync(Guid id, UpdatePromoCampaignRequest request, CancellationToken ct = default)
    {
        var entity = await _dbContext.PromoCampaigns.Include(x => x.Outlet).Include(x => x.Targets).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Promo campaign tidak ditemukan.");
        await EnsureOutletAccessibleAsync(entity.OutletId, ct);
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var exists = await _dbContext.PromoCampaigns.AnyAsync(x => x.OutletId == request.OutletId && x.Code == request.Code.Trim() && x.Id != id, ct);
            if (exists)
            {
                throw new InvalidOperationException("Kode promo sudah digunakan pada outlet ini.");
            }
        }

        entity.OutletId = request.OutletId;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        entity.Name = request.Name.Trim();
        entity.DiscountType = request.DiscountType;
        entity.DiscountValue = request.DiscountValue;
        entity.ScopeType = request.ScopeType;
        entity.MinimumSpend = request.MinimumSpend;
        entity.MaximumDiscountAmount = request.MaximumDiscountAmount;
        entity.StartAt = request.StartAt;
        entity.EndAt = request.EndAt;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.PromoCampaignTargets.RemoveRange(entity.Targets);
        entity.Targets = request.Targets.Select(target => new PromoCampaignTarget
        {
            Id = Guid.NewGuid(),
            PromoCampaignId = entity.Id,
            ProductId = target.ProductId,
            CategoryId = target.CategoryId
        }).ToList();
        await _dbContext.SaveChangesAsync(ct);
        return await GetPromoByIdAsync(id, ct);
    }

    public async Task<IReadOnlyList<VoucherDto>> GetVouchersAsync(Guid? outletId, CancellationToken ct = default)
    {
        var query = _dbContext.Vouchers.Include(x => x.Outlet).AsNoTracking().AsQueryable();
        if (outletId.HasValue)
        {
            await EnsureOutletAccessibleAsync(outletId.Value, ct);
            query = query.Where(x => x.OutletId == outletId.Value);
        }

        var entities = await query.OrderByDescending(x => x.UpdatedAt).ToListAsync(ct);
        return entities.Select(entity => new VoucherDto(
            entity.Id,
            entity.OutletId,
            entity.Outlet.Name,
            entity.Code,
            entity.Name,
            entity.DiscountType,
            entity.DiscountValue,
            entity.MinimumSpend,
            entity.MaximumDiscountAmount,
            entity.UsageLimitTotal,
            entity.UsageLimitPerCode,
            entity.UsedCount,
            entity.StartAt,
            entity.EndAt,
            entity.IsActive
        )).ToList();
    }

    public async Task<VoucherDto> CreateVoucherAsync(CreateVoucherRequest request, CancellationToken ct = default)
    {
        await EnsureOutletAccessibleAsync(request.OutletId, ct);
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await _dbContext.Vouchers.AnyAsync(x => x.OutletId == request.OutletId && x.Code == normalizedCode, ct);
        if (exists)
        {
            throw new InvalidOperationException("Kode voucher sudah digunakan pada outlet ini.");
        }

        var entity = new Voucher
        {
            Id = Guid.NewGuid(),
            OutletId = request.OutletId,
            Code = normalizedCode,
            Name = request.Name.Trim(),
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            MinimumSpend = request.MinimumSpend,
            MaximumDiscountAmount = request.MaximumDiscountAmount,
            UsageLimitTotal = request.UsageLimitTotal,
            UsageLimitPerCode = request.UsageLimitPerCode,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Vouchers.Add(entity);
        await _dbContext.SaveChangesAsync(ct);
        return await GetVoucherByIdAsync(entity.Id, ct);
    }

    public async Task<VoucherDto> UpdateVoucherAsync(Guid id, UpdateVoucherRequest request, CancellationToken ct = default)
    {
        var entity = await _dbContext.Vouchers.Include(x => x.Outlet).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Voucher tidak ditemukan.");
        await EnsureOutletAccessibleAsync(entity.OutletId, ct);
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await _dbContext.Vouchers.AnyAsync(x => x.OutletId == request.OutletId && x.Code == normalizedCode && x.Id != id, ct);
        if (exists)
        {
            throw new InvalidOperationException("Kode voucher sudah digunakan pada outlet ini.");
        }

        entity.OutletId = request.OutletId;
        entity.Code = normalizedCode;
        entity.Name = request.Name.Trim();
        entity.DiscountType = request.DiscountType;
        entity.DiscountValue = request.DiscountValue;
        entity.MinimumSpend = request.MinimumSpend;
        entity.MaximumDiscountAmount = request.MaximumDiscountAmount;
        entity.UsageLimitTotal = request.UsageLimitTotal;
        entity.UsageLimitPerCode = request.UsageLimitPerCode;
        entity.StartAt = request.StartAt;
        entity.EndAt = request.EndAt;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return await GetVoucherByIdAsync(id, ct);
    }

    public async Task<VoucherDto> SetVoucherActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var entity = await _dbContext.Vouchers.Include(x => x.Outlet).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Voucher tidak ditemukan.");
        await EnsureOutletAccessibleAsync(entity.OutletId, ct);
        entity.IsActive = isActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return await GetVoucherByIdAsync(id, ct);
    }

    private async Task EnsureOutletAccessibleAsync(Guid outletId, CancellationToken ct)
    {
        var outlet = await _dbContext.Outlets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == outletId, ct)
            ?? throw new InvalidOperationException("Outlet tidak ditemukan.");
        if (!outlet.IsActive)
        {
            throw new InvalidOperationException("Outlet tidak aktif.");
        }

        if (_currentUserService.Role != "Owner" && _currentUserService.OutletId != outletId)
        {
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke outlet tersebut.");
        }
    }

    private async Task<PromoCampaignDto> GetPromoByIdAsync(Guid id, CancellationToken ct)
    {
        var entity = await _dbContext.PromoCampaigns.Include(x => x.Outlet).Include(x => x.Targets).AsNoTracking().FirstAsync(x => x.Id == id, ct);
        return MapPromoCampaignDto(entity);
    }

    private async Task<VoucherDto> GetVoucherByIdAsync(Guid id, CancellationToken ct)
    {
        var entity = await _dbContext.Vouchers.Include(x => x.Outlet).AsNoTracking().FirstAsync(x => x.Id == id, ct);
        return new VoucherDto(
            entity.Id,
            entity.OutletId,
            entity.Outlet.Name,
            entity.Code,
            entity.Name,
            entity.DiscountType,
            entity.DiscountValue,
            entity.MinimumSpend,
            entity.MaximumDiscountAmount,
            entity.UsageLimitTotal,
            entity.UsageLimitPerCode,
            entity.UsedCount,
            entity.StartAt,
            entity.EndAt,
            entity.IsActive
        );
    }

    private static PromoCampaignDto MapPromoCampaignDto(PromoCampaign entity)
        => new(
            entity.Id,
            entity.OutletId,
            entity.Outlet.Name,
            entity.Code,
            entity.Name,
            entity.DiscountType,
            entity.DiscountValue,
            entity.ScopeType,
            entity.MinimumSpend,
            entity.MaximumDiscountAmount,
            entity.StartAt,
            entity.EndAt,
            entity.IsActive,
            entity.Targets.Where(target => target.ProductId.HasValue).Select(target => target.ProductId!.Value).ToList(),
            entity.Targets.Where(target => target.CategoryId.HasValue).Select(target => target.CategoryId!.Value).ToList()
        );

}
