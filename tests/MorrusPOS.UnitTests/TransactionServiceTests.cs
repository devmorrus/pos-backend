using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
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

        _dbContext = new AppDbContext(options);
        _stockServiceMock = Substitute.For<IStockService>();
        _currentUserMock = Substitute.For<ICurrentUserService>();
        _notificationServiceMock = Substitute.For<IPosNotificationService>();
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

        var service = new TransactionService(_dbContext, _stockServiceMock, _currentUserMock, _notificationServiceMock);

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

        // Assert
        result.Should().NotBeNull();
        result.TransactionNumber.Should().StartWith("TRX-");

        // Verify stock adjustment call
        await _stockServiceMock.Received(1).AddMovementAsync(
            productId: prodId,
            outletId: outletId,
            qtyChange: -2,
            movementType: "sale",
            referenceType: "transaction",
            referenceId: checkoutId,
            note: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        );

        // Verify SignalR broadcast
        await _notificationServiceMock.Received(1).SendStockUpdateAsync(
            outletId: outletId,
            updates: Arg.Is<List<StockUpdateItem>>(list => list.Count == 1 && list[0].ProductId == prodId && list[0].Qty == 2),
            ct: Arg.Any<CancellationToken>()
        );
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
        _dbContext.Outlets.Add(new Outlet { Id = outletId, Code = "OUT-1", Name = "Main Outlet", IsActive = true });
        _dbContext.Users.Add(new User { Id = userId, Name = "Kasir 1", Email = "k1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
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

        var service = new TransactionService(_dbContext, _stockServiceMock, _currentUserMock, _notificationServiceMock);
        var request = new CheckoutRequest(
            Id: trxId, // Duplicate ID
            OutletId: outletId,
            CashierSessionId: Guid.NewGuid(),
            Channel: "pos",
            Subtotal: 50000,
            DiscountTotal: 0,
            TaxTotal: 0,
            GrandTotal: 50000,
            Items: new List<CheckoutItemRequest>(),
            Payments: new List<PaymentRequest>()
        );

        // Act
        var result = await service.CheckoutAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.TransactionNumber.Should().Be("TRX-EXISTING"); // Directly returned existing without processing again
    }
}
