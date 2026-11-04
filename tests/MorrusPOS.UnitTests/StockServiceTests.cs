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

public class StockServiceTests
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public StockServiceTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, TestTenant.CurrentUser);
        _user = Substitute.For<ICurrentUserService>();
        _user.UserId.Returns(_userId);
        _db.Outlets.Add(new Outlet { Id = _outletId, Code = "OUT", Name = "Outlet", IsActive = true });
        _db.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "Category" });
        _db.Products.Add(new Product
        {
            Id = _productId, CategoryId = _db.Categories.Local.Single().Id,
            Sku = "SKU", Name = "Product", Unit = "pcs", IsActive = true,
            TrackingMode = ProductTrackingModes.Quantity
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task AddMovementAsync_Should_WriteLedgerAndProjectionExactlyOnce()
    {
        var service = new StockService(_db, _user);
        var operationId = Guid.NewGuid();

        await service.AddMovementAsync(_productId, _outletId, 5, StockMovementType.PurchaseIn,
            "test", Guid.NewGuid(), operationId: operationId);
        await _db.SaveChangesAsync();
        await service.AddMovementAsync(_productId, _outletId, 5, StockMovementType.PurchaseIn,
            "test", _db.StockLedgers.Single().ReferenceId, operationId: operationId);
        await _db.SaveChangesAsync();

        _db.StockLedgers.Should().ContainSingle();
        (await _db.InventoryStocks.SingleAsync()).QtyOnHand.Should().Be(5);
        _db.StockLedgers.Single().PostingKey.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(0, StockMovementType.PurchaseIn)]
    [InlineData(-1, StockMovementType.PurchaseIn)]
    [InlineData(1, StockMovementType.Sale)]
    [InlineData(1, "unknown")]
    public async Task AddMovementAsync_Should_RejectInvalidMovement(decimal qty, string movementType)
    {
        var service = new StockService(_db, _user);
        var act = () => service.AddMovementAsync(_productId, _outletId, qty, movementType,
            "test", Guid.NewGuid());

        await act.Should().ThrowAsync<StockIntegrityException>()
            .Where(x => x.Code == StockIntegrityErrorCodes.MovementInvalid);
    }

    [Fact]
    public async Task AddMovementAsync_Should_RejectSamePostingKeyWithDifferentPayload()
    {
        var service = new StockService(_db, _user);
        var referenceId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        await service.AddMovementAsync(_productId, _outletId, 2, StockMovementType.PurchaseIn,
            "test", referenceId, operationId: operationId);
        await _db.SaveChangesAsync();

        var act = () => service.AddMovementAsync(_productId, _outletId, 3, StockMovementType.PurchaseIn,
            "test", referenceId, operationId: operationId);

        await act.Should().ThrowAsync<StockIntegrityException>()
            .Where(x => x.Code == StockIntegrityErrorCodes.PostingConflict);
    }
}
