using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using Xunit;

namespace MorrusPOS.UnitTests;

public class BufferStockServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<(Guid outletId, Guid productId, Guid categoryId, Guid userId)> SeedBasicAsync(AppDbContext db)
    {
        var outletId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var catId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        db.Outlets.Add(new Outlet { Id = outletId, Code = "OUT-1", Name = "Outlet 1", IsActive = true });
        db.Users.Add(new User { Id = userId, Name = "Admin", Email = "admin@test.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        db.Categories.Add(new Category { Id = catId, Name = "Makanan" });
        db.Products.Add(new Product
        {
            Id = productId,
            CategoryId = catId,
            Sku = "SKU-1",
            Name = "Ayam Geprek",
            BasePrice = 15000,
            CostPrice = 10000,
            Unit = "pcs",
            IsActive = true
        });
        await db.SaveChangesAsync();
        return (outletId, productId, catId, userId);
    }

    [Theory]
    [InlineData(10, 0, true, 10)]
    [InlineData(10, 3, true, 7)]
    [InlineData(3, 3, true, 0)]
    [InlineData(2, 5, true, 0)]
    [InlineData(0, 0, true, 0)]
    [InlineData(10, 3, false, 10)]
    public void CalculateAvailableOnlineQty_Should_MatchFormula(
        decimal qtyOnHand, decimal bufferQty, bool isEnabled, decimal expected)
    {
        using var db = CreateDbContext();
        var service = new OnlineStockAvailabilityService(db);

        var result = service.CalculateAvailableOnlineQty(qtyOnHand, bufferQty, isEnabled);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task UpsertAsync_Should_CreatePolicy_WithoutTouchingStockLedger()
    {
        using var db = CreateDbContext();
        var (outletId, productId, _, userId) = await SeedBasicAsync(db);

        db.InventoryStocks.Add(new InventoryStock
        {
            Id = Guid.NewGuid(),
            OutletId = outletId,
            ProductId = productId,
            QtyOnHand = 10,
            MinStockAlert = 0,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var availability = new OnlineStockAvailabilityService(db);
        var service = new BufferStockService(db, availability);

        var result = await service.UpsertAsync(userId, new UpsertBufferStockRequest(
            outletId, productId, null, 3, true));

        result.QtyOnHand.Should().Be(10);
        result.BufferQty.Should().Be(3);
        result.AvailableOnlineQty.Should().Be(7);
        result.IsOnlineAvailable.Should().BeTrue();

        // Tidak boleh menyentuh stok fisik / ledger.
        db.StockLedgers.Should().BeEmpty();
        var stock = await db.InventoryStocks.FirstAsync();
        stock.QtyOnHand.Should().Be(10);
    }

    [Fact]
    public async Task UpsertAsync_Should_RejectNegativeBuffer()
    {
        using var db = CreateDbContext();
        var (outletId, productId, _, userId) = await SeedBasicAsync(db);

        var service = new BufferStockService(db, new OnlineStockAvailabilityService(db));

        var act = () => service.UpsertAsync(userId, new UpsertBufferStockRequest(
            outletId, productId, null, -1, true));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*negatif*");
    }

    [Fact]
    public async Task UpsertAsync_Should_RejectVariantOfOtherProduct()
    {
        using var db = CreateDbContext();
        var (outletId, productId, catId, userId) = await SeedBasicAsync(db);

        var otherProductId = Guid.NewGuid();
        db.Products.Add(new Product
        {
            Id = otherProductId,
            CategoryId = catId,
            Sku = "SKU-2",
            Name = "Kopi",
            BasePrice = 10000,
            CostPrice = 5000,
            Unit = "pcs",
            IsActive = true
        });
        var variantId = Guid.NewGuid();
        db.ProductVariants.Add(new ProductVariant
        {
            Id = variantId,
            ProductId = otherProductId,
            Sku = "KOPI-L",
            BasePrice = 12000,
            CostPrice = 6000,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = new BufferStockService(db, new OnlineStockAvailabilityService(db));

        var act = () => service.UpsertAsync(userId, new UpsertBufferStockRequest(
            outletId, productId, variantId, 1, true));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Varian*");
    }

    [Fact]
    public async Task GetAvailabilityAsync_Should_ClampToZero_When_BufferExceedsStock()
    {
        using var db = CreateDbContext();
        var (outletId, productId, _, userId) = await SeedBasicAsync(db);

        db.InventoryStocks.Add(new InventoryStock
        {
            Id = Guid.NewGuid(),
            OutletId = outletId,
            ProductId = productId,
            QtyOnHand = 2,
            MinStockAlert = 0,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new BufferStockService(db, new OnlineStockAvailabilityService(db));
        await service.UpsertAsync(userId, new UpsertBufferStockRequest(
            outletId, productId, null, 5, true));

        var availability = await service.GetAvailabilityAsync(outletId, productId, null);

        availability.AvailableOnlineQty.Should().Be(0);
        availability.IsOnlineAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetAvailabilityMapAsync_Should_UseVariantPolicy_NotParentFallback_When_VariantHasPolicy()
    {
        using var db = CreateDbContext();
        var (outletId, productId, _, userId) = await SeedBasicAsync(db);

        var variantId = Guid.NewGuid();
        db.ProductVariants.Add(new ProductVariant
        {
            Id = variantId,
            ProductId = productId,
            Sku = "AYAM-LVL3",
            BasePrice = 17000,
            CostPrice = 11000,
            IsActive = true
        });
        db.InventoryStocks.Add(new InventoryStock
        {
            Id = Guid.NewGuid(),
            OutletId = outletId,
            ProductId = productId,
            ProductVariantId = variantId,
            QtyOnHand = 5,
            MinStockAlert = 0,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var availabilityService = new OnlineStockAvailabilityService(db);
        var service = new BufferStockService(db, availabilityService);

        // Policy parent = 5 (akan membuat varian 0 jika dipakai),
        // policy varian = 1 (hasil harus 4).
        await service.UpsertAsync(userId, new UpsertBufferStockRequest(
            outletId, productId, null, 5, true));
        await service.UpsertAsync(userId, new UpsertBufferStockRequest(
            outletId, productId, variantId, 1, true));

        var map = await availabilityService.GetAvailabilityMapAsync(outletId);

        map[new OnlineStockKey(productId, variantId)].AvailableOnlineQty.Should().Be(4);
    }

    [Fact]
    public async Task GetByOutletAsync_Should_ReturnZeroBuffer_When_NoPolicy()
    {
        using var db = CreateDbContext();
        var (outletId, productId, _, _) = await SeedBasicAsync(db);

        db.InventoryStocks.Add(new InventoryStock
        {
            Id = Guid.NewGuid(),
            OutletId = outletId,
            ProductId = productId,
            QtyOnHand = 8,
            MinStockAlert = 0,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new BufferStockService(db, new OnlineStockAvailabilityService(db));
        var rows = await service.GetByOutletAsync(outletId);

        rows.Should().HaveCount(1);
        rows[0].BufferQty.Should().Be(0);
        rows[0].AvailableOnlineQty.Should().Be(8);
        rows[0].IsOnlineAvailable.Should().BeTrue();
    }
}
