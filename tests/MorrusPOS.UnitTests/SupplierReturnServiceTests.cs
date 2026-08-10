using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Suppliers;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace MorrusPOS.UnitTests;

public class SupplierReturnServiceTests
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserServiceMock;
    private readonly IStockService _stockServiceMock;
    private readonly IPosNotificationService _notificationServiceMock;

    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _roleId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _purchaseOrderId = Guid.NewGuid();

    public SupplierReturnServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new AppDbContext(options);
        _currentUserServiceMock = Substitute.For<ICurrentUserService>();
        _stockServiceMock = Substitute.For<IStockService>();
        _notificationServiceMock = Substitute.For<IPosNotificationService>();
        _currentUserServiceMock.Role.Returns("Owner");
        _currentUserServiceMock.OutletId.Returns((Guid?)null);
        _currentUserServiceMock.UserId.Returns(_userId);
    }

    private async Task SeedBaseDataAsync()
    {
        _dbContext.Outlets.Add(new Outlet { Id = _outletId, Code = "OUT-1", Name = "Main Outlet", IsActive = true });
        _dbContext.Suppliers.Add(new Supplier { Id = _supplierId, Name = "Supplier A", IsActive = true });
        _dbContext.Users.Add(new User { Id = _userId, Name = "Finance", Email = "finance@test.com", PasswordHash = "hash", RoleId = _roleId });
        _dbContext.Categories.Add(new Category { Id = _categoryId, Name = "Cat" });
        _dbContext.Products.Add(new Product
        {
            Id = _productId,
            CategoryId = _categoryId,
            Sku = "SKU-1",
            Name = "Product A",
            BasePrice = 10000,
            CostPrice = 7000,
            Unit = "pcs",
            IsActive = true
        });
        _dbContext.InventoryStocks.Add(new InventoryStock
        {
            Id = Guid.NewGuid(),
            OutletId = _outletId,
            ProductId = _productId,
            QtyOnHand = 10,
            MinStockAlert = 0
        });
        _dbContext.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = _purchaseOrderId,
            SupplierId = _supplierId,
            OutletId = _outletId,
            PoNumber = "PO-001",
            PoDate = DateTime.UtcNow.AddDays(-3),
            PaymentType = PurchaseOrderPaymentType.Tempo,
            Status = PurchaseOrderStatus.Completed,
            TotalAmount = 50000,
            DueDate = DateTime.UtcNow.AddDays(30),
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = DateTime.UtcNow.AddDays(-3),
            Items = new List<PurchaseOrderItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = _productId,
                    Qty = 5,
                    UnitCost = 10000,
                    TotalCost = 50000
                }
            }
        });
        _dbContext.SupplierDebts.Add(new SupplierDebt
        {
            Id = Guid.NewGuid(),
            SupplierId = _supplierId,
            PurchaseOrderId = _purchaseOrderId,
            DueDate = DateTime.UtcNow.AddDays(30),
            Amount = 50000,
            PaidAmount = 0,
            RemainingAmount = 50000,
            Status = SupplierDebtStatus.Unpaid,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = DateTime.UtcNow.AddDays(-3)
        });

        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_Should_CreateDraftSupplierReturn()
    {
        await SeedBaseDataAsync();
        var service = new SupplierReturnService(_dbContext, _currentUserServiceMock, _stockServiceMock, _notificationServiceMock);

        var result = await service.CreateAsync(_userId, new CreateSupplierReturnRequest(
            _supplierId,
            _purchaseOrderId,
            DateTime.UtcNow,
            "Barang rusak",
            new List<SupplierReturnItemRequest> { new(_productId, 2) }
        ));

        result.Status.Should().Be("draft");
        result.ReturnNumber.Should().StartWith("SR-");
        result.TotalAmount.Should().Be(20000);
        result.Items.Should().ContainSingle(item => item.ProductId == _productId && item.Qty == 2);
    }

    [Fact]
    public async Task CreateAsync_Should_RejectQtyAboveEligible()
    {
        await SeedBaseDataAsync();
        var service = new SupplierReturnService(_dbContext, _currentUserServiceMock, _stockServiceMock, _notificationServiceMock);

        var act = () => service.CreateAsync(_userId, new CreateSupplierReturnRequest(
            _supplierId,
            _purchaseOrderId,
            DateTime.UtcNow,
            null,
            new List<SupplierReturnItemRequest> { new(_productId, 6) }
        ));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*eligible*");
    }

    [Fact]
    public async Task UpdateStatusAsync_ToSent_Should_CreateStockMovement_AndReduceDebt()
    {
        await SeedBaseDataAsync();
        var service = new SupplierReturnService(_dbContext, _currentUserServiceMock, _stockServiceMock, _notificationServiceMock);
        var supplierReturn = await service.CreateAsync(_userId, new CreateSupplierReturnRequest(
            _supplierId,
            _purchaseOrderId,
            DateTime.UtcNow,
            null,
            new List<SupplierReturnItemRequest> { new(_productId, 2) }
        ));

        var result = await service.UpdateStatusAsync(_userId, supplierReturn.Id, new UpdateSupplierReturnStatusRequest("sent"));

        result.Status.Should().Be("sent");
        await _stockServiceMock.Received(1).AddMovementAsync(
            _productId,
            _outletId,
            -2,
            StockMovementType.ConsignmentReturn,
            "supplier_return",
            supplierReturn.Id,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        var debt = await _dbContext.SupplierDebts.FirstAsync(d => d.PurchaseOrderId == _purchaseOrderId);
        debt.RemainingAmount.Should().Be(30000);
        debt.Amount.Should().Be(30000);
    }
}
