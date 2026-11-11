using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Common.Events;
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
    private readonly IStockEventPublisher _notificationServiceMock;
    private readonly ICurrentUserService _currentUserServiceMock;

    public StockTransferServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new AppDbContext(options, TestTenant.CurrentUser);
        _stockServiceMock = Substitute.For<IStockService>();
        _notificationServiceMock = Substitute.For<IStockEventPublisher>();
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
        _dbContext.StockLocations.Add(new StockLocation { Id = fromOutletId, Type = StockLocationTypes.Outlet, Code = "LOC-1", Name = "Source", OutletId = fromOutletId });
        _dbContext.Outlets.Add(new Outlet { Id = toOutletId, Code = "OUT-2", Name = "Destination Outlet", IsActive = true });
        _dbContext.StockLocations.Add(new StockLocation { Id = toOutletId, Type = StockLocationTypes.Outlet, Code = "LOC-2", Name = "Destination", OutletId = toOutletId });
        _dbContext.Users.Add(new User { Id = userId, Name = "Admin 1", Email = "a1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        _dbContext.Products.Add(new Product { Id = prodId, CategoryId = catId, Sku = "SKU-1", Name = "Chiki", BasePrice = 10000, CostPrice = 8000, Unit = "pcs", IsActive = true });
        
        // Seed stock at Source: 10 units
        _dbContext.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), StockLocationId = fromOutletId, ProductId = prodId, QtyOnHand = 10, MinStockAlert = 0 });
        await _dbContext.SaveChangesAsync();

        var service = new StockTransferService(_dbContext, _stockServiceMock, _notificationServiceMock, _currentUserServiceMock);

        var request = new CreateStockTransferRequest(
            FromStockLocationId: fromOutletId,
            ToStockLocationId: toOutletId,
            Items: new List<StockTransferItemRequest>
            {
                new(prodId, Qty: 4)
            }
        );

        // Act
        var result = await service.CreateAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(StockTransferStatus.InTransit);
        result.TransferNumber.Should().StartWith("TRF-");
        result.Items.Should().HaveCount(1);

        // Verify stock was immediately deducted from source
        await _stockServiceMock.Received(1).AddMovementAsync(
            productId: prodId,
            stockLocationId: fromOutletId,
            qtyChange: -4,
            movementType: "transfer_out",
            referenceType: "stock_transfer",
            referenceId: result.Id,
            note: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>(),
            productVariantId: Arg.Any<Guid?>(),
            productBatchId: Arg.Any<Guid?>(),
            productSerialId: Arg.Any<Guid?>(),
            documentItemId: Arg.Any<Guid?>()
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
        _dbContext.StockLocations.Add(new StockLocation { Id = fromOutletId, Type = StockLocationTypes.Outlet, Code = "LOC-1", Name = "Source", OutletId = fromOutletId });
        _dbContext.Outlets.Add(new Outlet { Id = toOutletId, Code = "OUT-2", Name = "Destination Outlet", IsActive = true });
        _dbContext.StockLocations.Add(new StockLocation { Id = toOutletId, Type = StockLocationTypes.Outlet, Code = "LOC-2", Name = "Destination", OutletId = toOutletId });
        _dbContext.Users.Add(new User { Id = userId, Name = "Admin 1", Email = "a1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        _dbContext.Products.Add(new Product { Id = prodId, CategoryId = catId, Sku = "SKU-1", Name = "Chiki", BasePrice = 10000, CostPrice = 8000, Unit = "pcs", IsActive = true });
        
        // Seed insufficient stock: 2 units
        _dbContext.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), StockLocationId = fromOutletId, ProductId = prodId, QtyOnHand = 2, MinStockAlert = 0 });
        await _dbContext.SaveChangesAsync();

        var service = new StockTransferService(_dbContext, _stockServiceMock, _notificationServiceMock, _currentUserServiceMock);

        var request = new CreateStockTransferRequest(
            FromStockLocationId: fromOutletId,
            ToStockLocationId: toOutletId,
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
        _dbContext.StockLocations.Add(new StockLocation { Id = fromOutletId, Type = StockLocationTypes.Outlet, Code = "LOC-1", Name = "Source", OutletId = fromOutletId });
        _dbContext.Outlets.Add(new Outlet { Id = toOutletId, Code = "OUT-2", Name = "Destination Outlet", IsActive = true });
        _dbContext.StockLocations.Add(new StockLocation { Id = toOutletId, Type = StockLocationTypes.Outlet, Code = "LOC-2", Name = "Destination", OutletId = toOutletId });
        _dbContext.Users.Add(new User { Id = userId, Name = "Admin 1", Email = "a1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        _dbContext.Products.Add(new Product { Id = prodId, CategoryId = catId, Sku = "SKU-1", Name = "Chiki", BasePrice = 10000, CostPrice = 8000, Unit = "pcs", IsActive = true });
        
        // Seed stock at Source: 10 units
        _dbContext.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), StockLocationId = fromOutletId, ProductId = prodId, QtyOnHand = 10, MinStockAlert = 0 });
        await _dbContext.SaveChangesAsync();

        var service = new StockTransferService(_dbContext, _stockServiceMock, _notificationServiceMock, _currentUserServiceMock);

        var request = new CreateStockTransferRequest(
            FromStockLocationId: fromOutletId,
            ToStockLocationId: toOutletId,
            Items: new List<StockTransferItemRequest> { new(prodId, Qty: 4) }
        );
        var transfer = await service.CreateAsync(userId, request);

        // Act
        var result = await service.ApproveAsync(userId, transfer.Id);

        // Assert
        result.Status.Should().Be(StockTransferStatus.Received);

        // Verify destination addition movement
        await _stockServiceMock.Received(1).AddMovementAsync(
            productId: prodId,
            stockLocationId: toOutletId,
            qtyChange: 4,
            movementType: "transfer_in",
            referenceType: "stock_transfer",
            referenceId: transfer.Id,
            note: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>(),
            productVariantId: Arg.Any<Guid?>(),
            productBatchId: Arg.Any<Guid?>(),
            productSerialId: Arg.Any<Guid?>(),
            documentItemId: Arg.Any<Guid?>()
        );

        await _notificationServiceMock.Received(1).EnqueueCurrentStockAsync(
            toOutletId,
            Arg.Is<IReadOnlyCollection<StockEventKey>>(items => items != null && items.Count == 1 && items.Single().ProductId == prodId),
            $"stock:transfer:{transfer.Id:N}:received",
            Arg.Any<CancellationToken>());
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
        _dbContext.StockLocations.Add(new StockLocation { Id = fromOutletId, Type = StockLocationTypes.Outlet, Code = "LOC-1", Name = "Source", OutletId = fromOutletId });
        _dbContext.Outlets.Add(new Outlet { Id = toOutletId, Code = "OUT-2", Name = "Destination Outlet", IsActive = true });
        _dbContext.StockLocations.Add(new StockLocation { Id = toOutletId, Type = StockLocationTypes.Outlet, Code = "LOC-2", Name = "Destination", OutletId = toOutletId });
        _dbContext.Users.Add(new User { Id = userId, Name = "Admin 1", Email = "a1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        _dbContext.Products.Add(new Product { Id = prodId, CategoryId = catId, Sku = "SKU-1", Name = "Chiki", BasePrice = 10000, CostPrice = 8000, Unit = "pcs", IsActive = true });
        
        // Seed stock at Source: 10 units
        _dbContext.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), StockLocationId = fromOutletId, ProductId = prodId, QtyOnHand = 10, MinStockAlert = 0 });
        await _dbContext.SaveChangesAsync();

        var service = new StockTransferService(_dbContext, _stockServiceMock, _notificationServiceMock, _currentUserServiceMock);

        var request = new CreateStockTransferRequest(
            FromStockLocationId: fromOutletId,
            ToStockLocationId: toOutletId,
            Items: new List<StockTransferItemRequest> { new(prodId, Qty: 4) }
        );
        var transfer = await service.CreateAsync(userId, request);

        // Act
        var result = await service.RejectAsync(userId, transfer.Id);

        // Assert
        result.Status.Should().Be(StockTransferStatus.Returned);

        // Verify stock was restored/returned to FromOutlet (so it receives TransferIn of 4)
        await _stockServiceMock.Received(1).AddMovementAsync(
            productId: prodId,
            stockLocationId: fromOutletId,
            qtyChange: 4,
            movementType: "transfer_in",
            referenceType: "stock_transfer",
            referenceId: transfer.Id,
            note: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>(),
            productVariantId: Arg.Any<Guid?>(),
            productBatchId: Arg.Any<Guid?>(),
            productSerialId: Arg.Any<Guid?>(),
            documentItemId: Arg.Any<Guid?>()
        );
    }

    [Fact]
    public async Task CreateAndApproveBatchTransfer_Should_MoveQtyToDestinationBatch()
    {
        var userId = Guid.NewGuid();
        var fromOutletId = Guid.NewGuid();
        var toOutletId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var catId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        _dbContext.Outlets.Add(new Outlet { Id = fromOutletId, Code = "OUT-1", Name = "Source", IsActive = true });
        _dbContext.StockLocations.Add(new StockLocation { Id = fromOutletId, Type = StockLocationTypes.Outlet, Code = "LOC-1", Name = "Source", OutletId = fromOutletId });
        _dbContext.Outlets.Add(new Outlet { Id = toOutletId, Code = "OUT-2", Name = "Destination", IsActive = true });
        _dbContext.StockLocations.Add(new StockLocation { Id = toOutletId, Type = StockLocationTypes.Outlet, Code = "LOC-2", Name = "Destination", OutletId = toOutletId });
        _dbContext.Users.Add(new User { Id = userId, Name = "Admin 1", Email = "a1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        _dbContext.Products.Add(new Product
        {
            Id = prodId, CategoryId = catId, Sku = "SKU-B", Name = "Susu",
            BasePrice = 10000, CostPrice = 8000, Unit = "pcs", IsActive = true,
            TrackingMode = ProductTrackingModes.Batch
        });
        _dbContext.ProductBatches.Add(new ProductBatch
        {
            Id = batchId, ProductId = prodId, StockLocationId = fromOutletId,
            BatchNumber = "B-001", QtyProduction = 10, QtyRemaining = 10,
            ExpiryDate = DateTime.UtcNow.Date.AddDays(30)
        });
        await _dbContext.SaveChangesAsync();

        var service = new StockTransferService(_dbContext, _stockServiceMock, _notificationServiceMock, _currentUserServiceMock);
        var transfer = await service.CreateAsync(userId, new CreateStockTransferRequest(
            fromOutletId,
            toOutletId,
            new List<StockTransferItemRequest>
            {
                new(prodId, Qty: 3) { ProductBatchId = batchId }
            }));

        transfer.Items.Should().ContainSingle();
        transfer.Items[0].ProductBatchId.Should().Be(batchId);
        (await _dbContext.ProductBatches.SingleAsync(x => x.Id == batchId))
            .QtyRemaining.Should().Be(7);

        var approved = await service.ApproveAsync(userId, transfer.Id);

        approved.Status.Should().Be(StockTransferStatus.Received);
        var destinationBatch = await _dbContext.ProductBatches.SingleAsync(x =>
            x.StockLocationId == toOutletId && x.ProductId == prodId && x.BatchNumber == "B-001");
        destinationBatch.QtyRemaining.Should().Be(3);
        destinationBatch.QtyProduction.Should().Be(3);
    }

    [Fact]
    public async Task ReceiveAsync_Should_PostActualQty_And_RecordVariance()
    {
        var userId = Guid.NewGuid();
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        _dbContext.Outlets.AddRange(
            new Outlet { Id = fromId, Code = "SRC", Name = "Source", IsActive = true },
            new Outlet { Id = toId, Code = "DST", Name = "Destination", IsActive = true });
        _dbContext.StockLocations.AddRange(
            new StockLocation { Id = fromId, Type = StockLocationTypes.Outlet, Code = "SRC", Name = "Source", OutletId = fromId },
            new StockLocation { Id = toId, Type = StockLocationTypes.Outlet, Code = "DST", Name = "Destination", OutletId = toId });
        _dbContext.Users.Add(new User { Id = userId, Name = "Receiver", Email = "receiver@test.local", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = categoryId, Name = "Produk" });
        _dbContext.Products.Add(new Product
        {
            Id = productId, CategoryId = categoryId, Sku = "VAR-1", Name = "Produk Selisih",
            BasePrice = 10, CostPrice = 7, Unit = "pcs", IsActive = true
        });
        _dbContext.InventoryStocks.Add(new InventoryStock
        {
            Id = Guid.NewGuid(), StockLocationId = fromId, ProductId = productId, QtyOnHand = 10
        });
        await _dbContext.SaveChangesAsync();

        var service = new StockTransferService(_dbContext, _stockServiceMock, _notificationServiceMock, _currentUserServiceMock);
        var sent = await service.CreateAsync(userId, new CreateStockTransferRequest(
            fromId, toId, [new StockTransferItemRequest(productId, 4)]));

        var received = await service.ReceiveAsync(userId, sent.Id, new ReceiveStockTransferRequest(
            Guid.NewGuid(),
            [new ReceiveStockTransferItemRequest(sent.Items.Single().Id, 3, "short_shipped", "Satu unit tidak ada.")]));

        received.Status.Should().Be(StockTransferStatus.ReceivedWithVariance);
        received.HasVariance.Should().BeTrue();
        received.Items.Single().ReceivedQty.Should().Be(3);
        received.Items.Single().DifferenceQty.Should().Be(-1);
        await _stockServiceMock.Received(1).AddMovementAsync(
            productId, toId, 3, StockMovementType.TransferIn, "stock_transfer", sent.Id,
            Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
            Arg.Any<Guid?>(), sent.Items.Single().Id, Arg.Any<Guid?>());
    }
}
