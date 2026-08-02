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

public class PurchaseOrderServiceTests
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockServiceMock;

    // Fixed seed data
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _catId = Guid.NewGuid();
    private readonly Guid _prodId = Guid.NewGuid();
    private readonly Guid _roleId = Guid.NewGuid();

    public PurchaseOrderServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new AppDbContext(options);
        _stockServiceMock = Substitute.For<IStockService>();
    }

    private async Task SeedBaseDataAsync(decimal initialCostPrice = 8000m)
    {
        _dbContext.Outlets.Add(new Outlet { Id = _outletId, Code = "OUT-1", Name = "Main Outlet", IsActive = true });
        _dbContext.Suppliers.Add(new Supplier { Id = _supplierId, Name = "PT Maju Jaya", IsActive = true });
        _dbContext.Users.Add(new User { Id = _userId, Name = "Admin", Email = "a@test.com", PasswordHash = "hash", RoleId = _roleId });
        _dbContext.Categories.Add(new Category { Id = _catId, Name = "Minuman" });
        _dbContext.Products.Add(new Product
        {
            Id = _prodId,
            CategoryId = _catId,
            Sku = "SKU-ABC",
            Name = "Aqua Botol",
            BasePrice = 5000,
            CostPrice = initialCostPrice,
            Unit = "pcs",
            IsActive = true
        });
        _dbContext.InventoryStocks.Add(new InventoryStock
        {
            Id = Guid.NewGuid(),
            OutletId = _outletId,
            ProductId = _prodId,
            QtyOnHand = 0,
            MinStockAlert = 0
        });
        await _dbContext.SaveChangesAsync();
    }

    // ===== CREATE PO =====

    [Fact]
    public async Task CreateAsync_Should_CreatePO_WithStatusDraft_AndCalculateTotalAmount()
    {
        await SeedBaseDataAsync();
        var service = new PurchaseOrderService(_dbContext, _stockServiceMock);

        var request = new CreatePurchaseOrderRequest(
            SupplierId: _supplierId,
            OutletId: _outletId,
            PaymentType: "cash",
            DueDate: null,
            Items: new List<PurchaseOrderItemRequest>
            {
                new(_prodId, Qty: 10, UnitCost: 4500)
            }
        );

        var result = await service.CreateAsync(_userId, request);

        result.Should().NotBeNull();
        result.Status.Should().Be("draft");
        result.PoNumber.Should().StartWith("PO-");
        result.TotalAmount.Should().Be(45000); // 10 * 4500
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateAsync_Should_ThrowException_When_TempoPaymentHasNoDueDate()
    {
        await SeedBaseDataAsync();
        var service = new PurchaseOrderService(_dbContext, _stockServiceMock);

        var request = new CreatePurchaseOrderRequest(
            SupplierId: _supplierId,
            OutletId: _outletId,
            PaymentType: "tempo",
            DueDate: null, // Missing DueDate!
            Items: new List<PurchaseOrderItemRequest> { new(_prodId, Qty: 5, UnitCost: 3000) }
        );

        var act = () => service.CreateAsync(_userId, request);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*DueDate*");
    }

    // ===== UPDATE STATUS TO COMPLETED =====

    [Fact]
    public async Task UpdateStatusAsync_ToCompleted_Should_IncrementStock_And_UpdateCostPrice_And_CreateSupplierDebt_ForTempoPO()
    {
        await SeedBaseDataAsync(initialCostPrice: 8000); // Initial HPP = 8000
        var service = new PurchaseOrderService(_dbContext, _stockServiceMock);

        // Create a Tempo PO
        var createRequest = new CreatePurchaseOrderRequest(
            SupplierId: _supplierId,
            OutletId: _outletId,
            PaymentType: "tempo",
            DueDate: DateTime.UtcNow.AddDays(30),
            Items: new List<PurchaseOrderItemRequest>
            {
                new(_prodId, Qty: 20, UnitCost: 4000) // New HPP will be 4000
            }
        );
        var po = await service.CreateAsync(_userId, createRequest);

        // Act: Complete the PO
        var result = await service.UpdateStatusAsync(_userId, po.Id, new UpdatePoStatusRequest("completed"));

        // Assert: status updated
        result.Status.Should().Be("completed");

        // Assert: stock service was called to add purchase_in movement
        await _stockServiceMock.Received(1).AddMovementAsync(
            productId: _prodId,
            outletId: _outletId,
            qtyChange: 20,
            movementType: "purchase_in",
            referenceType: "purchase_order",
            referenceId: po.Id,
            note: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        );

        // Assert: HPP was updated
        var updatedProduct = await _dbContext.Products.FindAsync(_prodId);
        updatedProduct!.CostPrice.Should().Be(4000);

        // Assert: AuditLog was written for HPP change
        var auditLog = await _dbContext.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == _prodId && a.Action == "cost_price_update");
        auditLog.Should().NotBeNull();

        // Assert: SupplierDebt was automatically created for tempo
        var debt = await _dbContext.SupplierDebts.FirstOrDefaultAsync(d => d.PurchaseOrderId == po.Id);
        debt.Should().NotBeNull();
        debt!.Amount.Should().Be(80000); // 20 * 4000
        debt.Status.Should().Be("unpaid");
        debt.RemainingAmount.Should().Be(80000);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToCompleted_Should_NOT_CreateSupplierDebt_ForCashPO()
    {
        await SeedBaseDataAsync();
        var service = new PurchaseOrderService(_dbContext, _stockServiceMock);

        var createRequest = new CreatePurchaseOrderRequest(
            SupplierId: _supplierId,
            OutletId: _outletId,
            PaymentType: "cash",
            DueDate: null,
            Items: new List<PurchaseOrderItemRequest> { new(_prodId, Qty: 10, UnitCost: 3000) }
        );
        var po = await service.CreateAsync(_userId, createRequest);

        await service.UpdateStatusAsync(_userId, po.Id, new UpdatePoStatusRequest("completed"));

        // No debt should be created for cash payment
        var debt = await _dbContext.SupplierDebts.FirstOrDefaultAsync(d => d.PurchaseOrderId == po.Id);
        debt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_Should_ThrowException_When_PoIsAlreadyCompleted()
    {
        await SeedBaseDataAsync();
        var service = new PurchaseOrderService(_dbContext, _stockServiceMock);

        var createRequest = new CreatePurchaseOrderRequest(
            SupplierId: _supplierId,
            OutletId: _outletId,
            PaymentType: "cash",
            DueDate: null,
            Items: new List<PurchaseOrderItemRequest> { new(_prodId, Qty: 5, UnitCost: 2000) }
        );
        var po = await service.CreateAsync(_userId, createRequest);

        // Complete first time
        await service.UpdateStatusAsync(_userId, po.Id, new UpdatePoStatusRequest("completed"));

        // Try to complete again — should throw
        var act = () => service.UpdateStatusAsync(_userId, po.Id, new UpdatePoStatusRequest("completed"));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*sudah berstatus*");
    }
}
