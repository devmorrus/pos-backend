using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace MorrusPOS.UnitTests;

public class StockReconciliationServiceTests
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IStockEventPublisher _notifications;
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public StockReconciliationServiceTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, TestTenant.CurrentUser);
        _user = Substitute.For<ICurrentUserService>();
        _notifications = Substitute.For<IStockEventPublisher>();
        _user.UserId.Returns(_userId);
        _user.Role.Returns("Admin");
        _user.OutletId.Returns(_outletId);
        var categoryId = Guid.NewGuid();
        _db.Outlets.Add(new Outlet { Id = _outletId, Code = "OUT", Name = "Outlet", IsActive = true });
        _db.Categories.Add(new Category { Id = categoryId, Name = "Category" });
        _db.Users.Add(new User { Id = _userId, Name = "Admin", Email = "admin@test", PasswordHash = "x", RoleId = Guid.NewGuid(), OutletId = _outletId });
        _db.Products.Add(new Product
        {
            Id = _productId, CategoryId = categoryId, Sku = "SKU", Name = "Product",
            Unit = "pcs", IsActive = true, TrackingMode = ProductTrackingModes.Quantity
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task PreviewAndRepair_Should_RebuildBalanceWithoutChangingLedger()
    {
        SeedMismatch(ledgerQty: 5, storedQty: 2);
        var service = new StockReconciliationService(_db, _user, _notifications);

        var preview = await service.PreviewAsync(_userId, _outletId);
        preview.MismatchCount.Should().Be(1);
        preview.Items[0].Difference.Should().Be(3);

        var repaired = await service.RepairAsync(_userId, _outletId, "Repair cache stock");
        repaired.RepairedCount.Should().Be(1);
        (await _db.InventoryStocks.SingleAsync()).QtyOnHand.Should().Be(5);
        (await _db.StockLedgers.CountAsync()).Should().Be(1);

        (await service.PreviewAsync(_userId, _outletId)).MismatchCount.Should().Be(0);
    }

    [Fact]
    public async Task Repair_Should_Block_WhenBatchProjectionDoesNotMatchLedger()
    {
        _db.Products.Single().TrackingMode = ProductTrackingModes.Batch;
        SeedMismatch(ledgerQty: 5, storedQty: 2);
        _db.ProductBatches.Add(new ProductBatch
        {
            Id = Guid.NewGuid(), ProductId = _productId, OutletId = _outletId,
            BatchNumber = "B-1", QtyProduction = 4, QtyRemaining = 4
        });
        await _db.SaveChangesAsync();
        var service = new StockReconciliationService(_db, _user, _notifications);

        var act = () => service.RepairAsync(_userId, _outletId, "Repair batch cache");

        await act.Should().ThrowAsync<StockIntegrityException>()
            .Where(x => x.Code == StockIntegrityErrorCodes.ReconciliationBlocked);
        (await _db.InventoryStocks.SingleAsync()).QtyOnHand.Should().Be(2);
        (await _db.StockReconciliationRuns.SingleAsync()).Status.Should().Be(StockReconciliationStatus.Blocked);
    }

    private void SeedMismatch(decimal ledgerQty, decimal storedQty)
    {
        _db.StockLedgers.Add(new StockLedger
        {
            Id = Guid.NewGuid(), ProductId = _productId, OutletId = _outletId,
            PostingKey = $"test/{Guid.NewGuid():N}", MovementType = StockMovementType.OpeningBalance,
            QtyChange = ledgerQty, ReferenceType = "test", ReferenceId = Guid.NewGuid(),
            CreatedBy = _userId, CreatedAt = DateTime.UtcNow
        });
        _db.InventoryStocks.Add(new InventoryStock
        {
            Id = Guid.NewGuid(), ProductId = _productId, OutletId = _outletId,
            QtyOnHand = storedQty, MinStockAlert = 0
        });
        _db.SaveChanges();
    }
}
