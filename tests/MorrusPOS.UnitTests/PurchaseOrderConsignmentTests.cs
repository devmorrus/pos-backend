using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

public class PurchaseOrderConsignmentTests
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockServiceMock;
    private readonly ICurrentUserService _currentUserServiceMock;

    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _catId = Guid.NewGuid();
    private readonly Guid _prodId = Guid.NewGuid();
    private readonly Guid _roleId = Guid.NewGuid();

    public PurchaseOrderConsignmentTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new AppDbContext(options);
        _stockServiceMock = Substitute.For<IStockService>();
        _currentUserServiceMock = Substitute.For<ICurrentUserService>();
        _currentUserServiceMock.Role.Returns("Owner");
        _currentUserServiceMock.OutletId.Returns((Guid?)null);
        _currentUserServiceMock.UserId.Returns(_userId);
    }

    private async Task SeedBaseDataAsync()
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
            CostPrice = 3000,
            Unit = "pcs",
            IsActive = true,
            IsConsignment = false
        });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CompletePO_WithConsignmentPayment_Should_CreateConsignmentReceipt_AndFlagProducts()
    {
        // Arrange
        await SeedBaseDataAsync();
        var service = new PurchaseOrderService(_dbContext, _stockServiceMock, _currentUserServiceMock);

        var request = new CreatePurchaseOrderRequest(
            SupplierId: _supplierId,
            OutletId: _outletId,
            PaymentType: "consignment",
            DueDate: null,
            Items: new List<PurchaseOrderItemRequest>
            {
                new(_prodId, Qty: 10, UnitCost: 3500)
            }
        );

        var poDto = await service.CreateAsync(_userId, request);

        // Act
        var result = await service.UpdateStatusAsync(_userId, poDto.Id, new UpdatePoStatusRequest("completed"));

        // Assert
        result.Status.Should().Be("completed");

        // Verify Product IsConsignment flag updated
        var updatedProduct = await _dbContext.Products.FindAsync(_prodId);
        updatedProduct.Should().NotBeNull();
        updatedProduct!.IsConsignment.Should().BeTrue();

        // Verify Consignment receipt record created
        var consignmentReceipt = await _dbContext.Consignments
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.SupplierId == _supplierId && c.OutletId == _outletId);

        consignmentReceipt.Should().NotBeNull();
        consignmentReceipt!.ConsignmentNumber.Should().Be($"CSG-PO-{result.PoNumber}");
        consignmentReceipt.Status.Should().Be("received");
        consignmentReceipt.Items.Should().HaveCount(1);
        consignmentReceipt.Items.First().ProductId.Should().Be(_prodId);
        consignmentReceipt.Items.First().Qty.Should().Be(10);
        consignmentReceipt.Items.First().UnitCost.Should().Be(3500);

        // Verify stock service was called with consignment_in movement type
        await _stockServiceMock.Received(1).AddMovementAsync(
            productId: _prodId,
            outletId: _outletId,
            qtyChange: 10,
            movementType: "consignment_in",
            referenceType: "consignment",
            referenceId: consignmentReceipt.Id,
            note: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        );

        // Verify no SupplierDebt was created
        var debts = await _dbContext.SupplierDebts.ToListAsync();
        debts.Should().BeEmpty();
    }
}
