using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace MorrusPOS.UnitTests;

public class StockOpnameServiceTests
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockServiceMock;
    private readonly IPosNotificationService _notificationServiceMock;

    public StockOpnameServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new AppDbContext(options);
        _stockServiceMock = Substitute.For<IStockService>();
        _notificationServiceMock = Substitute.For<IPosNotificationService>();
    }

    [Fact]
    public async Task CreateAsync_Should_AdjustStockAndSendNotification_When_VarianceIsNonZero()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var catId = Guid.NewGuid();

        _dbContext.Outlets.Add(new Outlet { Id = outletId, Code = "OUT-1", Name = "Main Outlet", IsActive = true });
        _dbContext.Users.Add(new User { Id = userId, Name = "Admin 1", Email = "a1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        _dbContext.Products.Add(new Product { Id = prodId, CategoryId = catId, Sku = "SKU-1", Name = "Chiki", BasePrice = 10000, CostPrice = 8000, Unit = "pcs", IsActive = true });
        _dbContext.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), OutletId = outletId, ProductId = prodId, QtyOnHand = 10, MinStockAlert = 0 });
        await _dbContext.SaveChangesAsync();

        var service = new StockOpnameService(_dbContext, _stockServiceMock, _notificationServiceMock);

        var request = new CreateStockOpnameRequest(
            OutletId: outletId,
            Items: new List<StockOpnameItemRequest>
            {
                new(prodId, PhysicalQty: 15) // Variance: 15 - 10 = +5
            }
        );

        // Act
        var result = await service.CreateAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].Variance.Should().Be(5);

        // Verify stock service call
        await _stockServiceMock.Received(1).AddMovementAsync(
            productId: prodId,
            outletId: outletId,
            qtyChange: 5,
            movementType: "opname_adjustment",
            referenceType: "stock_opname",
            referenceId: result.Id,
            note: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        );

        // Verify SignalR notification call
        await _notificationServiceMock.Received(1).SendStockUpdateAsync(
            outletId: outletId,
            updates: Arg.Is<List<StockUpdateItem>>(list => list != null && list.Count == 1 && list[0].ProductId == prodId && list[0].Qty == 5),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task CreateAsync_Should_NotAdjustStock_When_VarianceIsZero()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var catId = Guid.NewGuid();

        _dbContext.Outlets.Add(new Outlet { Id = outletId, Code = "OUT-1", Name = "Main Outlet", IsActive = true });
        _dbContext.Users.Add(new User { Id = userId, Name = "Admin 1", Email = "a1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        _dbContext.Products.Add(new Product { Id = prodId, CategoryId = catId, Sku = "SKU-1", Name = "Chiki", BasePrice = 10000, CostPrice = 8000, Unit = "pcs", IsActive = true });
        _dbContext.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), OutletId = outletId, ProductId = prodId, QtyOnHand = 10, MinStockAlert = 0 });
        await _dbContext.SaveChangesAsync();

        var service = new StockOpnameService(_dbContext, _stockServiceMock, _notificationServiceMock);

        var request = new CreateStockOpnameRequest(
            OutletId: outletId,
            Items: new List<StockOpnameItemRequest>
            {
                new(prodId, PhysicalQty: 10) // Variance: 10 - 10 = 0
            }
        );

        // Act
        var result = await service.CreateAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Items[0].Variance.Should().Be(0);

        // Verify NO stock service or notification call
        await _stockServiceMock.DidNotReceiveWithAnyArgs().AddMovementAsync(
            productId: default,
            outletId: default,
            qtyChange: default,
            movementType: default!,
            referenceType: default!,
            referenceId: default,
            note: default,
            ct: default
        );

        await _notificationServiceMock.DidNotReceiveWithAnyArgs().SendStockUpdateAsync(
            outletId: default,
            updates: default!,
            ct: default
        );
    }
}
