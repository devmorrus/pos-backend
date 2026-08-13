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

public class StockTransferServiceTests
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockServiceMock;
    private readonly IPosNotificationService _notificationServiceMock;
    private readonly ICurrentUserService _currentUserServiceMock;

    public StockTransferServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new AppDbContext(options);
        _stockServiceMock = Substitute.For<IStockService>();
        _notificationServiceMock = Substitute.For<IPosNotificationService>();
        _currentUserServiceMock = Substitute.For<ICurrentUserService>();
        _currentUserServiceMock.Role.Returns("Admin");
    }

    [Fact]
    public async Task CreateAsync_Should_DeductStockFromSource_When_StockIsAvailable()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fromOutletId = Guid.NewGuid();
        var toOutletId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var catId = Guid.NewGuid();

        _dbContext.Outlets.Add(new Outlet { Id = fromOutletId, Code = "OUT-1", Name = "Source Outlet", IsActive = true });
        _dbContext.Outlets.Add(new Outlet { Id = toOutletId, Code = "OUT-2", Name = "Destination Outlet", IsActive = true });
        _dbContext.Users.Add(new User { Id = userId, Name = "Admin 1", Email = "a1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        _dbContext.Products.Add(new Product { Id = prodId, CategoryId = catId, Sku = "SKU-1", Name = "Chiki", BasePrice = 10000, CostPrice = 8000, Unit = "pcs", IsActive = true });
        
        // Seed stock at Source: 10 units
        _dbContext.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), OutletId = fromOutletId, ProductId = prodId, QtyOnHand = 10, MinStockAlert = 0 });
        await _dbContext.SaveChangesAsync();

        var service = new StockTransferService(_dbContext, _stockServiceMock, _notificationServiceMock, _currentUserServiceMock);

        var request = new CreateStockTransferRequest(
            FromOutletId: fromOutletId,
            ToOutletId: toOutletId,
            Items: new List<StockTransferItemRequest>
            {
                new(prodId, Qty: 4)
            }
        );

        // Act
        var result = await service.CreateAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("pending");
        result.TransferNumber.Should().StartWith("TRF-");
        result.Items.Should().HaveCount(1);

        // Verify stock was immediately deducted from source
        await _stockServiceMock.Received(1).AddMovementAsync(
            productId: prodId,
            outletId: fromOutletId,
            qtyChange: -4,
            movementType: "transfer_out",
            referenceType: "stock_transfer",
            referenceId: result.Id,
            note: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task CreateAsync_Should_ThrowException_When_StockIsInsufficientAtSource()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fromOutletId = Guid.NewGuid();
        var toOutletId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var catId = Guid.NewGuid();

        _dbContext.Outlets.Add(new Outlet { Id = fromOutletId, Code = "OUT-1", Name = "Source Outlet", IsActive = true });
        _dbContext.Outlets.Add(new Outlet { Id = toOutletId, Code = "OUT-2", Name = "Destination Outlet", IsActive = true });
        _dbContext.Users.Add(new User { Id = userId, Name = "Admin 1", Email = "a1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        _dbContext.Products.Add(new Product { Id = prodId, CategoryId = catId, Sku = "SKU-1", Name = "Chiki", BasePrice = 10000, CostPrice = 8000, Unit = "pcs", IsActive = true });
        
        // Seed insufficient stock: 2 units
        _dbContext.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), OutletId = fromOutletId, ProductId = prodId, QtyOnHand = 2, MinStockAlert = 0 });
        await _dbContext.SaveChangesAsync();

        var service = new StockTransferService(_dbContext, _stockServiceMock, _notificationServiceMock, _currentUserServiceMock);

        var request = new CreateStockTransferRequest(
            FromOutletId: fromOutletId,
            ToOutletId: toOutletId,
            Items: new List<StockTransferItemRequest> { new(prodId, Qty: 5) } // Request 5, only 2 available
        );

        // Act & Assert
        Func<Task> act = () => service.CreateAsync(userId, request);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Stok tidak mencukupi*");
    }

    [Fact]
    public async Task ApproveAsync_Should_AddStockToDestination()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fromOutletId = Guid.NewGuid();
        var toOutletId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var catId = Guid.NewGuid();

        _dbContext.Outlets.Add(new Outlet { Id = fromOutletId, Code = "OUT-1", Name = "Source Outlet", IsActive = true });
        _dbContext.Outlets.Add(new Outlet { Id = toOutletId, Code = "OUT-2", Name = "Destination Outlet", IsActive = true });
        _dbContext.Users.Add(new User { Id = userId, Name = "Admin 1", Email = "a1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        _dbContext.Products.Add(new Product { Id = prodId, CategoryId = catId, Sku = "SKU-1", Name = "Chiki", BasePrice = 10000, CostPrice = 8000, Unit = "pcs", IsActive = true });
        
        // Seed stock at Source: 10 units
        _dbContext.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), OutletId = fromOutletId, ProductId = prodId, QtyOnHand = 10, MinStockAlert = 0 });
        await _dbContext.SaveChangesAsync();

        var service = new StockTransferService(_dbContext, _stockServiceMock, _notificationServiceMock, _currentUserServiceMock);

        var request = new CreateStockTransferRequest(
            FromOutletId: fromOutletId,
            ToOutletId: toOutletId,
            Items: new List<StockTransferItemRequest> { new(prodId, Qty: 4) }
        );
        var transfer = await service.CreateAsync(userId, request);

        // Act
        var result = await service.ApproveAsync(userId, transfer.Id);

        // Assert
        result.Status.Should().Be("approved");

        // Verify destination addition movement
        await _stockServiceMock.Received(1).AddMovementAsync(
            productId: prodId,
            outletId: toOutletId,
            qtyChange: 4,
            movementType: "transfer_in",
            referenceType: "stock_transfer",
            referenceId: transfer.Id,
            note: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        );

        // Verify notification sent to destination outlet
        await _notificationServiceMock.Received(1).SendStockUpdateAsync(
            outletId: toOutletId,
            updates: Arg.Is<List<StockUpdateItem>>(list => list != null && list.Count == 1 && list[0].ProductId == prodId && list[0].Qty == 4),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task RejectAsync_Should_RestoreStockToSource()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fromOutletId = Guid.NewGuid();
        var toOutletId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var catId = Guid.NewGuid();

        _dbContext.Outlets.Add(new Outlet { Id = fromOutletId, Code = "OUT-1", Name = "Source Outlet", IsActive = true });
        _dbContext.Outlets.Add(new Outlet { Id = toOutletId, Code = "OUT-2", Name = "Destination Outlet", IsActive = true });
        _dbContext.Users.Add(new User { Id = userId, Name = "Admin 1", Email = "a1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        _dbContext.Products.Add(new Product { Id = prodId, CategoryId = catId, Sku = "SKU-1", Name = "Chiki", BasePrice = 10000, CostPrice = 8000, Unit = "pcs", IsActive = true });
        
        // Seed stock at Source: 10 units
        _dbContext.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), OutletId = fromOutletId, ProductId = prodId, QtyOnHand = 10, MinStockAlert = 0 });
        await _dbContext.SaveChangesAsync();

        var service = new StockTransferService(_dbContext, _stockServiceMock, _notificationServiceMock, _currentUserServiceMock);

        var request = new CreateStockTransferRequest(
            FromOutletId: fromOutletId,
            ToOutletId: toOutletId,
            Items: new List<StockTransferItemRequest> { new(prodId, Qty: 4) }
        );
        var transfer = await service.CreateAsync(userId, request);

        // Act
        var result = await service.RejectAsync(userId, transfer.Id);

        // Assert
        result.Status.Should().Be("rejected");

        // Verify stock was restored/returned to FromOutlet (so it receives TransferIn of 4)
        await _stockServiceMock.Received(1).AddMovementAsync(
            productId: prodId,
            outletId: fromOutletId,
            qtyChange: 4,
            movementType: "transfer_in",
            referenceType: "stock_transfer",
            referenceId: transfer.Id,
            note: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
}
