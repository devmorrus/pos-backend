using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using Xunit;

namespace MorrusPOS.UnitTests;

public class PricingServiceTests
{
    private readonly AppDbContext _dbContext;
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly Guid _taxFreeProductId = Guid.NewGuid();
    private readonly Guid _taxedProductId = Guid.NewGuid();

    public PricingServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);
    }

    private async Task SeedAsync()
    {
        _dbContext.Outlets.Add(new Outlet
        {
            Id = _outletId,
            Code = "OUT-PRICING",
            Name = "Outlet Pricing",
            IsActive = true
        });
        _dbContext.Categories.Add(new Category
        {
            Id = _categoryId,
            Name = "Food"
        });
        _dbContext.Products.AddRange(
            new Product
            {
                Id = _taxedProductId,
                CategoryId = _categoryId,
                Sku = "SKU-TAX",
                Name = "Taxed Product",
                BasePrice = 100000,
                CostPrice = 60000,
                Unit = "pcs",
                IsActive = true,
                IsTaxable = true,
                IsServiceChargeable = true
            },
            new Product
            {
                Id = _taxFreeProductId,
                CategoryId = _categoryId,
                Sku = "SKU-FREE",
                Name = "Tax Free Product",
                BasePrice = 50000,
                CostPrice = 30000,
                Unit = "pcs",
                IsActive = true,
                IsTaxable = false,
                IsServiceChargeable = false
            });
        _dbContext.TaxRules.Add(new TaxRule
        {
            Id = Guid.NewGuid(),
            OutletId = _outletId,
            Name = "PPN 11",
            Rate = 11,
            IsActive = true,
            AppliesBeforeServiceCharge = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _dbContext.ServiceChargeRules.Add(new ServiceChargeRule
        {
            Id = Guid.NewGuid(),
            OutletId = _outletId,
            Name = "Service 5",
            Rate = 5,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _dbContext.PromoCampaigns.Add(new PromoCampaign
        {
            Id = Guid.NewGuid(),
            OutletId = _outletId,
            Code = "PROMO10",
            Name = "Promo 10%",
            DiscountType = PricingDiscountType.Percentage,
            DiscountValue = 10,
            ScopeType = PromoScopeType.Transaction,
            MinimumSpend = 10000,
            StartAt = DateTime.UtcNow.AddDays(-1),
            EndAt = DateTime.UtcNow.AddDays(1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _dbContext.Vouchers.Add(new Voucher
        {
            Id = Guid.NewGuid(),
            OutletId = _outletId,
            Code = "HEMAT15",
            Name = "Voucher 15%",
            DiscountType = PricingDiscountType.Percentage,
            DiscountValue = 15,
            MinimumSpend = 50000,
            UsageLimitTotal = 1,
            UsageLimitPerCode = 1,
            UsedCount = 0,
            StartAt = DateTime.UtcNow.AddDays(-1),
            EndAt = DateTime.UtcNow.AddDays(1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CalculateAsync_Should_ApplyExclusiveTaxAndServiceCharge_WithProductOverrides()
    {
        await SeedAsync();
        var service = new PricingService(_dbContext);

        var result = await service.CalculateAsync(new PricingPreviewRequest(
            _outletId,
            TransactionChannel.GrabFood,
            null,
            null,
            new List<CheckoutItemRequest>
            {
                new(_taxedProductId, 1, 100000, 0),
                new(_taxFreeProductId, 1, 50000, 0),
            }));

        result.Subtotal.Should().Be(150000);
        result.ServiceChargeTotal.Should().Be(5000);
        result.TaxTotal.Should().Be(11000);
        result.GrandTotal.Should().Be(166000);
    }

    [Fact]
    public async Task CalculateAsync_Should_ChooseVoucherOverPromo_WhenVoucherDiscountIsBetter()
    {
        await SeedAsync();
        var service = new PricingService(_dbContext);

        var result = await service.CalculateAsync(new PricingPreviewRequest(
            _outletId,
            TransactionChannel.Pos,
            "HEMAT15",
            null,
            new List<CheckoutItemRequest>
            {
                new(_taxedProductId, 1, 100000, 0),
            }));

        result.PromoDiscountTotal.Should().Be(0);
        result.VoucherDiscountTotal.Should().Be(15000);
        result.AppliedVoucher.Should().NotBeNull();
        result.AppliedPromo.Should().BeNull();
    }

    [Fact]
    public async Task CalculateAsync_Should_RejectVoucher_WhenMinimumSpendNotMet()
    {
        await SeedAsync();
        var service = new PricingService(_dbContext);

        var act = () => service.CalculateAsync(new PricingPreviewRequest(
            _outletId,
            TransactionChannel.Pos,
            "HEMAT15",
            null,
            new List<CheckoutItemRequest>
            {
                new(_taxFreeProductId, 1, 5000, 0),
            }));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*minimum belanja*");
    }
}
