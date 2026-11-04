using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace MorrusPOS.UnitTests;

public class TransactionServiceTests
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockServiceMock;
    private readonly ICurrentUserService _currentUserMock;
    private readonly IPosNotificationService _notificationServiceMock;

    public TransactionServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new AppDbContext(options, TestTenant.CurrentUser);
        _stockServiceMock = Substitute.For<IStockService>();
        _currentUserMock = Substitute.For<ICurrentUserService>();
        _notificationServiceMock = Substitute.For<IPosNotificationService>();
        _currentUserMock.Role.Returns("Admin");
        _currentUserMock.BusinessId.Returns(TestTenant.BusinessId);
    }

    [Fact]
    public async Task CheckoutAsync_Should_Succeed_And_DeductStock_When_StockIsAvailable()
    {
        // Arrange
        var outletId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var catId = Guid.NewGuid();

        _dbContext.Outlets.Add(new Outlet { Id = outletId, Code = "OUT-1", Name = "Main Outlet", IsActive = true });
        _dbContext.Users.Add(new User { Id = userId, Name = "Kasir 1", Email = "k1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        _dbContext.Products.Add(new Product { Id = prodId, CategoryId = catId, Sku = "SKU-1", Name = "Chiki", BasePrice = 10000, CostPrice = 8000, Unit = "pcs", IsActive = true });
        _dbContext.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), OutletId = outletId, ProductId = prodId, QtyOnHand = 10, MinStockAlert = 0 });
        _dbContext.CashierSessions.Add(new CashierSession { Id = sessionId, UserId = userId, OutletId = outletId, OpeningCash = 100000, Status = "open" });
        await _dbContext.SaveChangesAsync();

        _currentUserMock.UserId.Returns(userId);
        _currentUserMock.OutletId.Returns(outletId);

        var service = new TransactionService(_dbContext, _stockServiceMock, _currentUserMock, _notificationServiceMock,
            idempotencyService: new IdempotencyService(_dbContext, _currentUserMock));

        var checkoutId = Guid.NewGuid();
        var request = new CheckoutRequest(
            Id: checkoutId,
            OutletId: outletId,
            CashierSessionId: sessionId,
            Channel: "pos",
            Subtotal: 20000,
            DiscountTotal: 0,
            TaxTotal: 0,
            GrandTotal: 20000,
            Items: new List<CheckoutItemRequest>
            {
                new(prodId, Qty: 2, UnitPrice: 10000, DiscountAmount: 0)
            },
            Payments: new List<PaymentRequest>
            {
                new("cash", Amount: 20000, ReferenceNumber: null)
            }
        );

        // Act
        var result = await service.CheckoutAsync(request);
        var replay = await service.CheckoutAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.TransactionNumber.Should().StartWith("TRX-");
        replay.Id.Should().Be(result.Id);
        (await _dbContext.Transactions.CountAsync()).Should().Be(1);
        (await _dbContext.Payments.CountAsync()).Should().Be(1);
        (await _dbContext.IdempotencyRecords.CountAsync()).Should().Be(1);

        var conflict = () => service.CheckoutAsync(request with { GrandTotal = 20001 });
        await conflict.Should().ThrowAsync<IdempotencyException>()
            .Where(x => x.Code == IdempotencyErrorCodes.KeyReused);

        // Verify stock adjustment call
        await _stockServiceMock.Received(1).AddMovementAsync(
            productId: prodId,
            outletId: outletId,
            qtyChange: -2,
            movementType: "sale",
            referenceType: "transaction",
            referenceId: checkoutId,
            note: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>(),
            productVariantId: Arg.Is<Guid?>(value => value == null),
            productBatchId: Arg.Is<Guid?>(value => value == null),
            productSerialId: Arg.Is<Guid?>(value => value == null),
            documentItemId: Arg.Is<Guid?>(value => value.HasValue)
        );

        // Verify SignalR broadcast
        await _notificationServiceMock.Received(1).SendStockUpdateAsync(
            outletId: outletId,
            updates: Arg.Is<List<StockUpdateItem>>(list => list != null && list.Count == 1 && list[0].ProductId == prodId && list[0].Qty == 2),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task CheckoutAsync_Should_SellSerial_And_ReturnAllocationInDto()
    {
        var outletId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var catId = Guid.NewGuid();
        var serialId = Guid.NewGuid();

        _dbContext.Outlets.Add(new Outlet { Id = outletId, Code = "OUT-1", Name = "Main Outlet", IsActive = true });
        _dbContext.Users.Add(new User { Id = userId, Name = "Kasir 1", Email = "k1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Electronics" });
        _dbContext.Products.Add(new Product
        {
            Id = prodId, CategoryId = catId, Sku = "SKU-S", Name = "Phone",
            BasePrice = 10000000, CostPrice = 9000000, Unit = "pcs", IsActive = true,
            TrackingMode = ProductTrackingModes.Serial
        });
        _dbContext.InventoryStocks.Add(new InventoryStock
        {
            Id = Guid.NewGuid(), OutletId = outletId, ProductId = prodId, QtyOnHand = 1, MinStockAlert = 0
        });
        _dbContext.ProductSerials.Add(new ProductSerial
        {
            Id = serialId, ProductId = prodId, OutletId = outletId,
            SerialNumber = "IMEI-1", NormalizedSerialNumber = "IMEI-1",
            Status = ProductSerialStatuses.Available
        });
        _dbContext.CashierSessions.Add(new CashierSession
        {
            Id = sessionId, UserId = userId, OutletId = outletId, OpeningCash = 100000, Status = "open"
        });
        await _dbContext.SaveChangesAsync();

        _currentUserMock.UserId.Returns(userId);
        _currentUserMock.OutletId.Returns(outletId);

        var service = new TransactionService(_dbContext, new StockService(_dbContext, _currentUserMock), _currentUserMock, _notificationServiceMock,
            idempotencyService: new IdempotencyService(_dbContext, _currentUserMock));
        var checkoutId = Guid.NewGuid();
        var request = new CheckoutRequest(
            Id: checkoutId,
            OutletId: outletId,
            CashierSessionId: sessionId,
            Channel: "pos",
            Subtotal: 10000000,
            DiscountTotal: 0,
            TaxTotal: 0,
            GrandTotal: 10000000,
            Items: new List<CheckoutItemRequest>
            {
                new(prodId, Qty: 1, UnitPrice: 10000000, DiscountAmount: 0)
                {
                    SerialIds = new[] { serialId }
                }
            },
            Payments: new List<PaymentRequest>
            {
                new("cash", Amount: 10000000, ReferenceNumber: null)
            });

        var result = await service.CheckoutAsync(request);

        result.Items.Should().ContainSingle();
        result.Items[0].TrackingMode.Should().Be(ProductTrackingModes.Serial);
        result.Items[0].Serials.Should().ContainSingle(s => s.ProductSerialId == serialId && s.SerialNumber == "IMEI-1");
        (await _dbContext.ProductSerials.SingleAsync(x => x.Id == serialId))
            .Status.Should().Be(ProductSerialStatuses.Sold);

        var ledger = await _dbContext.StockLedgers.SingleAsync(x =>
            x.ProductSerialId == serialId && x.MovementType == StockMovementType.Sale);
        ledger.QtyChange.Should().Be(-1);
        ledger.DocumentItemId.Should().Be(result.Items[0].Id);

        var refundRequest = new RefundTransactionRequest(
            "refund", "Customer return", [new RefundTransactionItemRequest(prodId, 1)
            {
                TransactionItemId = result.Items[0].Id,
                SerialIds = [serialId]
            }], Guid.NewGuid());
        var refunded = await service.RefundAsync(result.Id, refundRequest);
        var refundReplay = await service.RefundAsync(result.Id, refundRequest);
        refunded.Items[0].Serials.Should().ContainSingle(x => x.ProductSerialId == serialId && x.IsReturned);
        refundReplay.Id.Should().Be(result.Id);
        (await _dbContext.Returns.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RefundAsync_Should_RejectBatchThatWasNotSoldByTransactionItem()
    {
        var outletId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var soldBatchId = Guid.NewGuid();
        var otherBatchId = Guid.NewGuid();

        _dbContext.Outlets.Add(new Outlet { Id = outletId, Code = "OUT-1", Name = "Main Outlet", IsActive = true });
        _dbContext.Users.Add(new User { Id = userId, Name = "Admin", Email = "admin@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = categoryId, Name = "Food" });
        _dbContext.Products.Add(new Product
        {
            Id = productId, CategoryId = categoryId, Sku = "SKU-B", Name = "Milk",
            BasePrice = 10000, CostPrice = 8000, Unit = "pcs", IsActive = true,
            TrackingMode = ProductTrackingModes.Batch
        });
        _dbContext.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), OutletId = outletId, ProductId = productId, QtyOnHand = 2 });
        _dbContext.ProductBatches.AddRange(
            new ProductBatch { Id = soldBatchId, ProductId = productId, OutletId = outletId, BatchNumber = "SOLD", QtyProduction = 1, QtyRemaining = 1 },
            new ProductBatch { Id = otherBatchId, ProductId = productId, OutletId = outletId, BatchNumber = "OTHER", QtyProduction = 1, QtyRemaining = 1 });
        _dbContext.CashierSessions.Add(new CashierSession { Id = sessionId, UserId = userId, OutletId = outletId, OpeningCash = 0, Status = "open" });
        await _dbContext.SaveChangesAsync();

        _currentUserMock.UserId.Returns(userId);
        _currentUserMock.OutletId.Returns(outletId);
        var service = new TransactionService(_dbContext, new StockService(_dbContext, _currentUserMock), _currentUserMock, _notificationServiceMock);
        var checkout = await service.CheckoutAsync(new CheckoutRequest(
            Guid.NewGuid(), outletId, sessionId, "pos", 10000, 0, 0, 10000,
            [new CheckoutItemRequest(productId, 1, 10000, 0)
                { BatchAllocations = [new BatchAllocationRequest(soldBatchId, 1)] }],
            [new PaymentRequest("cash", 10000, null)]));

        var act = () => service.RefundAsync(checkout.Id, new RefundTransactionRequest(
            "refund", "Wrong batch", [new RefundTransactionItemRequest(productId, 1)
            {
                TransactionItemId = checkout.Items[0].Id,
                BatchAllocations = [new BatchAllocationRequest(otherBatchId, 1)]
            }], Guid.NewGuid()));

        var exception = await act.Should().ThrowAsync<TrackingValidationException>();
        exception.Which.Code.Should().Be(TrackingErrorCodes.TrackingAllocationMismatch);
        (await _dbContext.ProductBatches.SingleAsync(x => x.Id == otherBatchId)).QtyRemaining.Should().Be(1);
    }

    [Fact]
    public async Task CheckoutAsync_Should_ThrowException_When_StockIsInsufficient()
    {
        // Arrange
        var outletId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var catId = Guid.NewGuid();

        _dbContext.Outlets.Add(new Outlet { Id = outletId, Code = "OUT-1", Name = "Main Outlet", IsActive = true });
        _dbContext.Users.Add(new User { Id = userId, Name = "Kasir 1", Email = "k1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        _dbContext.Products.Add(new Product { Id = prodId, CategoryId = catId, Sku = "SKU-1", Name = "Chiki", BasePrice = 10000, CostPrice = 8000, Unit = "pcs", IsActive = true });
        _dbContext.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), OutletId = outletId, ProductId = prodId, QtyOnHand = 1, MinStockAlert = 0 }); // only 1 stock
        _dbContext.CashierSessions.Add(new CashierSession { Id = sessionId, UserId = userId, OutletId = outletId, OpeningCash = 100000, Status = "open" });
        await _dbContext.SaveChangesAsync();

        _currentUserMock.UserId.Returns(userId);
        _currentUserMock.OutletId.Returns(outletId);

        var service = new TransactionService(_dbContext, _stockServiceMock, _currentUserMock, _notificationServiceMock);

        var request = new CheckoutRequest(
            Id: Guid.NewGuid(),
            OutletId: outletId,
            CashierSessionId: sessionId,
            Channel: "pos",
            Subtotal: 20000,
            DiscountTotal: 0,
            TaxTotal: 0,
            GrandTotal: 20000,
            Items: new List<CheckoutItemRequest>
            {
                new(prodId, Qty: 2, UnitPrice: 10000, DiscountAmount: 0) // request 2
            },
            Payments: new List<PaymentRequest>
            {
                new("cash", Amount: 20000, ReferenceNumber: null)
            }
        );

        // Act & Assert
        var act = () => service.CheckoutAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Stok tidak mencukupi*");
    }

    [Fact]
    public async Task CheckoutAsync_Should_ReturnExistingTransaction_When_GUIDIsDuplicate()
    {
        // Arrange
        var trxId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var catId = Guid.NewGuid();
        _dbContext.Outlets.Add(new Outlet { Id = outletId, Code = "OUT-1", Name = "Main Outlet", IsActive = true });
        _dbContext.Users.Add(new User { Id = userId, Name = "Kasir 1", Email = "k1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        _dbContext.Products.Add(new Product { Id = prodId, CategoryId = catId, Sku = "SKU-1", Name = "Chiki", BasePrice = 10000, CostPrice = 8000, Unit = "pcs", IsActive = true });
        _dbContext.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), OutletId = outletId, ProductId = prodId, QtyOnHand = 10, MinStockAlert = 0 });
        _dbContext.CashierSessions.Add(new CashierSession { Id = sessionId, UserId = userId, OutletId = outletId, OpeningCash = 100000, Status = "open" });
        _dbContext.Transactions.Add(new Transaction
        {
            Id = trxId,
            OutletId = outletId,
            UserId = userId,
            TransactionNumber = "TRX-EXISTING",
            Channel = "pos",
            Status = "completed",
            GrandTotal = 50000
        });
        await _dbContext.SaveChangesAsync();

        _currentUserMock.UserId.Returns(userId);
        _currentUserMock.OutletId.Returns(outletId);

        var service = new TransactionService(_dbContext, _stockServiceMock, _currentUserMock, _notificationServiceMock);
        var request = new CheckoutRequest(
            Id: trxId, // Duplicate ID
            OutletId: outletId,
            CashierSessionId: sessionId,
            Channel: "pos",
            Subtotal: 10000,
            DiscountTotal: 0,
            TaxTotal: 0,
            GrandTotal: 10000,
            Items: new List<CheckoutItemRequest>
            {
                new(prodId, Qty: 1, UnitPrice: 10000, DiscountAmount: 0)
            },
            Payments: new List<PaymentRequest>
            {
                new("cash", Amount: 10000, ReferenceNumber: null)
            }
        );

        // Act
        var result = await service.CheckoutAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.TransactionNumber.Should().Be("TRX-EXISTING"); // Directly returned existing without processing again
    }
}
