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

public class ConsignmentReturnServiceTests
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

    public ConsignmentReturnServiceTests()
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

    private async Task SeedBaseDataAsync(decimal initialStock = 10m)
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
            CostPrice = 1000,
            Unit = "pcs",
            IsActive = true,
            IsConsignment = true // Start as true for return
        });

        _dbContext.InventoryStocks.Add(new InventoryStock
        {
            ProductId = _productId,
            OutletId = _outletId,
            QtyOnHand = initialStock
        });

        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_Should_CreateDraftConsignmentReturn()
    {
        await SeedBaseDataAsync();
        var service = new ConsignmentReturnService(_dbContext, _stockServiceMock, _notificationServiceMock, _currentUserMock);

        var request = new CreateConsignmentReturnRequest(
            SupplierId: _supplierId,
            OutletId: _outletId,
            Items: new List<ConsignmentReturnItemRequest>
            {
                new(_productId, Qty: 5)
            }
        );

        var result = await service.CreateAsync(_userId, request);

        result.Should().NotBeNull();
        result.Status.Should().Be("draft");
        result.ReturnNumber.Should().StartWith("RTN-CSG-");
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToCompleted_Should_Fail_If_Stock_Insufficient()
    {
        await SeedBaseDataAsync(initialStock: 3);
        var service = new ConsignmentReturnService(_dbContext, _stockServiceMock, _notificationServiceMock, _currentUserMock);

        var request = new CreateConsignmentReturnRequest(
            SupplierId: _supplierId,
            OutletId: _outletId,
            Items: new List<ConsignmentReturnItemRequest>
            {
                new(_productId, Qty: 5) // 5 > 3
            }
        );

        var draft = await service.CreateAsync(_userId, request);

        Func<Task> act = () => service.UpdateStatusAsync(_userId, draft.Id, new UpdateConsignmentReturnStatusRequest("completed"));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tidak mencukupi untuk diretur*");
    }

    [Fact]
    public async Task UpdateStatusAsync_ToCompleted_Should_ReduceStock_And_SetCompleted()
    {
        await SeedBaseDataAsync(initialStock: 10);
        var service = new ConsignmentReturnService(_dbContext, _stockServiceMock, _notificationServiceMock, _currentUserMock);

        var request = new CreateConsignmentReturnRequest(
            SupplierId: _supplierId,
            OutletId: _outletId,
            Items: new List<ConsignmentReturnItemRequest>
            {
                new(_productId, Qty: 8) // 8 <= 10
            }
        );

        var draft = await service.CreateAsync(_userId, request);

        var result = await service.UpdateStatusAsync(_userId, draft.Id, new UpdateConsignmentReturnStatusRequest("completed"));

        result.Status.Should().Be("completed");
        await _stockServiceMock.Received(1).AddMovementAsync(
            productId: _productId,
            outletId: _outletId,
            qtyChange: -8m,
            movementType: StockMovementType.ConsignmentReturn,
            referenceType: "consignment_return",
            referenceId: draft.Id,
            note: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
}
