using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Consignments;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace MorrusPOS.UnitTests;

public class ConsignmentServiceTests
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockServiceMock;
    private readonly IPosNotificationService _notificationServiceMock;
    private readonly ICurrentUserService _currentUserMock;

    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _catId = Guid.NewGuid();
    private readonly Guid _roleId = Guid.NewGuid();

    public ConsignmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new AppDbContext(options);
        _stockServiceMock = Substitute.For<IStockService>();
        _notificationServiceMock = Substitute.For<IPosNotificationService>();
        _currentUserMock = Substitute.For<ICurrentUserService>();
        _currentUserMock.OutletId.Returns((Guid?)null);
    }

    private async Task SeedBaseDataAsync(decimal initialCostPrice = 1000m)
    {
        _dbContext.Outlets.Add(new Outlet { Id = _outletId, Code = "OUT-A", Name = "Outlet A", IsActive = true });
        _dbContext.Suppliers.Add(new Supplier { Id = _supplierId, Name = "Supplier A", IsActive = true });
        _dbContext.Users.Add(new User { Id = _userId, Name = "User A", Email = "u@a.com", PasswordHash = "hash", RoleId = _roleId });
        _dbContext.Categories.Add(new Category { Id = _catId, Name = "Cat A" });
        _dbContext.Products.Add(new Product
        {
            Id = _productId,
            CategoryId = _catId,
            Sku = "SKU-C",
            Name = "Consignment Product",
            BasePrice = 3000,
            CostPrice = initialCostPrice,
            Unit = "pcs",
            IsActive = true,
            IsConsignment = false // Start as false to test auto-flag
        });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_Should_CreateDraftConsignment()
    {
        await SeedBaseDataAsync();
        var service = new ConsignmentService(_dbContext, _stockServiceMock, _notificationServiceMock, _currentUserMock);

        var request = new CreateConsignmentRequest(
            SupplierId: _supplierId,
            OutletId: _outletId,
            Items: new List<ConsignmentItemRequest>
            {
                new(_productId, Qty: 10, UnitCost: 800, UnitPrice: 2000)
            }
        );

        var result = await service.CreateAsync(_userId, request);

        result.Should().NotBeNull();
        result.Status.Should().Be("draft");
        result.ConsignmentNumber.Should().StartWith("CSG-");
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToReceived_Should_AddStock_UpdateCostPrice_And_SetIsConsignment_And_BroadcastSignalR()
    {
        await SeedBaseDataAsync(initialCostPrice: 1000);
        var service = new ConsignmentService(_dbContext, _stockServiceMock, _notificationServiceMock, _currentUserMock);

        var createReq = new CreateConsignmentRequest(
            SupplierId: _supplierId,
            OutletId: _outletId,
            Items: new List<ConsignmentItemRequest>
            {
                new(_productId, Qty: 15, UnitCost: 750, UnitPrice: 2000)
            }
        );
        var consignment = await service.CreateAsync(_userId, createReq);

        // Act: Complete consignment receipt
        var result = await service.UpdateStatusAsync(_userId, consignment.Id, new UpdateConsignmentStatusRequest("received"));

        // Assert status
        result.Status.Should().Be("received");

        // Assert product updated to consignment and CostPrice updated to 750
        var product = await _dbContext.Products.FindAsync(_productId);
        product!.IsConsignment.Should().BeTrue();
        product.CostPrice.Should().Be(750);

        // Assert AuditLog created
        var log = await _dbContext.AuditLogs.FirstOrDefaultAsync(l => l.EntityId == _productId && l.Action == "cost_price_update");
        log.Should().NotBeNull();

        // Assert Stock movement added
        await _stockServiceMock.Received(1).AddMovementAsync(
            productId: _productId,
            outletId: _outletId,
            qtyChange: 15,
            movementType: "consignment_in",
            referenceType: "consignment",
            referenceId: consignment.Id,
            note: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        );

        // Assert SignalR broadcast sent
        await _notificationServiceMock.Received(1).SendStockUpdateAsync(
            outletId: _outletId,
            updates: Arg.Is<List<StockUpdateItem>>(list => list != null && list.Count == 1 && list[0].ProductId == _productId && list[0].Qty == 15),
            ct: Arg.Any<CancellationToken>()
        );
    }
}
