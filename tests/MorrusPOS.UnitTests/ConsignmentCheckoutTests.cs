using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Application.Features.Consignments;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace MorrusPOS.UnitTests;

public class ConsignmentCheckoutTests
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockServiceMock;
    private readonly ICurrentUserService _currentUserServiceMock;
    private readonly IPosNotificationService _notificationServiceMock;

    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _catId = Guid.NewGuid();
    private readonly Guid _roleId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();

    public ConsignmentCheckoutTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new AppDbContext(options);
        _stockServiceMock = Substitute.For<IStockService>();
        _currentUserServiceMock = Substitute.For<ICurrentUserService>();
        _notificationServiceMock = Substitute.For<IPosNotificationService>();

        _currentUserServiceMock.UserId.Returns(_userId);
        _currentUserServiceMock.OutletId.Returns(_outletId);
        _currentUserServiceMock.Role.Returns("Admin");
    }

    private async Task SeedBaseDataAsync(bool isConsignment = true, bool hasReceivedConsignment = true)
    {
        _dbContext.Outlets.Add(new Outlet { Id = _outletId, Code = "OUT-A", Name = "Outlet A", IsActive = true });
        _dbContext.Suppliers.Add(new Supplier { Id = _supplierId, Name = "Supplier A", IsActive = true });
        _dbContext.Users.Add(new User { Id = _userId, Name = "User A", Email = "u@a.com", PasswordHash = "hash", RoleId = _roleId });
        _dbContext.Categories.Add(new Category { Id = _catId, Name = "Cat A" });
        
        var product = new Product
        {
            Id = _productId,
            CategoryId = _catId,
            Sku = "SKU-C",
            Name = "Consignment Product",
            BasePrice = 3000,
            CostPrice = 1000,
            Unit = "pcs",
            IsActive = true,
            IsConsignment = isConsignment
        };
        _dbContext.Products.Add(product);

        _dbContext.InventoryStocks.Add(new InventoryStock
        {
            Id = Guid.NewGuid(),
            OutletId = _outletId,
            ProductId = _productId,
            QtyOnHand = 50,
            MinStockAlert = 0
        });

        _dbContext.CashierSessions.Add(new CashierSession
        {
            Id = _sessionId,
            OutletId = _outletId,
            UserId = _userId,
            Status = CashierSessionStatus.Open,
            OpeningCash = 100000,
            OpeningTime = DateTime.UtcNow
        });

        if (hasReceivedConsignment)
        {
            var consignment = new Consignment
            {
                Id = Guid.NewGuid(),
                SupplierId = _supplierId,
                OutletId = _outletId,
                ConsignmentNumber = "CSG-TEST-001",
                Status = ConsignmentStatus.Received,
                CreatedBy = _userId,
                ReceiveDate = DateTime.UtcNow
            };
            _dbContext.Consignments.Add(consignment);

            var item = new ConsignmentItem
            {
                Id = Guid.NewGuid(),
                ConsignmentId = consignment.Id,
                ProductId = _productId,
                Qty = 100,
                UnitCost = 800, // Dynamic Cost (Bagi hasil)
                UnitPrice = 3000
            };
            _dbContext.ConsignmentItems.Add(item);
        }

        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CheckoutAsync_Should_RecordConsignmentSale_WithCorrectSupplierAndUnitCost_When_ProductIsConsignment()
    {
        await SeedBaseDataAsync(isConsignment: true, hasReceivedConsignment: true);
        var trxService = new TransactionService(_dbContext, _stockServiceMock, _currentUserServiceMock, _notificationServiceMock);

        var trxId = Guid.NewGuid();
        var request = new CheckoutRequest(
            Id: trxId,
            OutletId: _outletId,
            CashierSessionId: _sessionId,
            Channel: "walk_in",
            Subtotal: 6000,
            DiscountTotal: 0,
            TaxTotal: 0,
            GrandTotal: 6000,
            Items: new List<CheckoutItemRequest>
            {
                new(_productId, Qty: 2, UnitPrice: 3000, DiscountAmount: 0)
            },
            Payments: new List<PaymentRequest>
            {
                new("cash", Amount: 6000, ReferenceNumber: null)
            }
        );

        // Act
        var result = await trxService.CheckoutAsync(request);

        result.Should().NotBeNull();
        result.Items[0].UnitCost.Should().Be(800); // Dynamic UnitCost loaded from received consignment instead of product's default 1000

        // Assert: ConsignmentSale is recorded
        var consignmentSale = await _dbContext.ConsignmentSales
            .FirstOrDefaultAsync(cs => cs.TransactionItem.TransactionId == trxId);

        consignmentSale.Should().NotBeNull();
        consignmentSale!.SupplierId.Should().Be(_supplierId);
        consignmentSale.Qty.Should().Be(2);
        consignmentSale.UnitCost.Should().Be(800);
        consignmentSale.TotalAmount.Should().Be(1600); // 2 * 800
        consignmentSale.Status.Should().Be("unpaid");
    }

    [Fact]
    public async Task CheckoutAsync_Should_ThrowException_When_ConsignmentProductHasNoReceivedReceipt()
    {
        // Set hasReceivedConsignment to false
        await SeedBaseDataAsync(isConsignment: true, hasReceivedConsignment: false);
        var trxService = new TransactionService(_dbContext, _stockServiceMock, _currentUserServiceMock, _notificationServiceMock);

        var trxId = Guid.NewGuid();
        var request = new CheckoutRequest(
            Id: trxId,
            OutletId: _outletId,
            CashierSessionId: _sessionId,
            Channel: "walk_in",
            Subtotal: 3000,
            DiscountTotal: 0,
            TaxTotal: 0,
            GrandTotal: 3000,
            Items: new List<CheckoutItemRequest>
            {
                new(_productId, Qty: 1, UnitPrice: 3000, DiscountAmount: 0)
            },
            Payments: new List<PaymentRequest>
            {
                new("cash", Amount: 3000, ReferenceNumber: null)
            }
        );

        var act = () => trxService.CheckoutAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tidak memiliki tanda terima konsinyasi*");
    }
}
