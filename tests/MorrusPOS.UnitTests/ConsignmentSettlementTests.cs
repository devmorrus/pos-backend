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

public class ConsignmentSettlementTests
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserMock;

    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _catId = Guid.NewGuid();
    private readonly Guid _roleId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();

    public ConsignmentSettlementTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new AppDbContext(options);
        _currentUserMock = Substitute.For<ICurrentUserService>();
        _currentUserMock.OutletId.Returns((Guid?)null);
    }

    private async Task SeedSalesDataAsync()
    {
        _dbContext.Outlets.Add(new Outlet { Id = _outletId, Code = "OUT-A", Name = "Outlet A", IsActive = true });
        _dbContext.Suppliers.Add(new Supplier { Id = _supplierId, Name = "Supplier A", IsActive = true });
        _dbContext.Users.Add(new User { Id = _userId, Name = "User A", Email = "u@a.com", PasswordHash = "hash", RoleId = _roleId });
        _dbContext.Categories.Add(new Category { Id = _catId, Name = "Cat A" });
        _dbContext.Products.Add(new Product { Id = _productId, CategoryId = _catId, Sku = "SKU", Name = "Product", IsConsignment = true, IsActive = true, Unit = "pcs" });
        
        var trx = new Transaction
        {
            Id = Guid.NewGuid(),
            OutletId = _outletId,
            UserId = _userId,
            CashierSessionId = _sessionId,
            TransactionNumber = "TRX-001",
            GrandTotal = 3000
        };
        _dbContext.Transactions.Add(trx);

        var item = new TransactionItem
        {
            Id = Guid.NewGuid(),
            TransactionId = trx.Id,
            ProductId = _productId,
            Qty = 3,
            UnitPrice = 1000,
            UnitCost = 600
        };
        _dbContext.TransactionItems.Add(item);

        // Add 2 unpaid consignment sales
        _dbContext.ConsignmentSales.Add(new ConsignmentSale
        {
            Id = Guid.NewGuid(),
            SupplierId = _supplierId,
            TransactionItemId = item.Id,
            Qty = 2,
            UnitCost = 600,
            TotalAmount = 1200,
            Status = ConsignmentSaleStatus.Unpaid
        });

        _dbContext.ConsignmentSales.Add(new ConsignmentSale
        {
            Id = Guid.NewGuid(),
            SupplierId = _supplierId,
            TransactionItemId = item.Id,
            Qty = 1,
            UnitCost = 600,
            TotalAmount = 600,
            Status = ConsignmentSaleStatus.Unpaid
        });

        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateSettlementAsync_Should_CreateDraftSettlement_WithAllUnpaidSalesLinked()
    {
        await SeedSalesDataAsync();
        var service = new ConsignmentSettlementService(_dbContext, _currentUserMock);

        var result = await service.CreateSettlementAsync(_userId, new CreateConsignmentSettlementRequest(_supplierId, _outletId));

        result.Should().NotBeNull();
        result.Status.Should().Be("draft");
        result.TotalAmount.Should().Be(1800); // 1200 + 600
        result.Sales.Should().HaveCount(2);
        result.SettlementNumber.Should().StartWith("SET-");

        // Sales in DB should now be linked
        var sales = await _dbContext.ConsignmentSales.Where(s => s.ConsignmentSettlementId == result.Id).ToListAsync();
        sales.Should().HaveCount(2);
        sales.All(s => s.Status == "unpaid").Should().BeTrue();
    }

    [Fact]
    public async Task UpdateStatusAsync_ToSettled_Should_SetStatusPaidForSalesAndSettlement()
    {
        await SeedSalesDataAsync();
        var service = new ConsignmentSettlementService(_dbContext, _currentUserMock);

        var settlement = await service.CreateSettlementAsync(_userId, new CreateConsignmentSettlementRequest(_supplierId, _outletId));

        // Act: settle the payment
        var result = await service.UpdateStatusAsync(_userId, settlement.Id, new UpdateConsignmentSettlementStatusRequest("settled"));

        result.Status.Should().Be("settled");
        result.Sales.All(s => s.Status == "paid").Should().BeTrue();

        // Verify in db
        var dbSales = await _dbContext.ConsignmentSales.Where(s => s.ConsignmentSettlementId == settlement.Id).ToListAsync();
        dbSales.All(s => s.Status == "paid").Should().BeTrue();
    }

    [Fact]
    public async Task UpdateStatusAsync_ToCancelled_Should_ReleaseLinkedSalesAndKeepThemUnpaid()
    {
        await SeedSalesDataAsync();
        var service = new ConsignmentSettlementService(_dbContext, _currentUserMock);

        var settlement = await service.CreateSettlementAsync(_userId, new CreateConsignmentSettlementRequest(_supplierId, _outletId));

        // Act: cancel the settlement
        var result = await service.UpdateStatusAsync(_userId, settlement.Id, new UpdateConsignmentSettlementStatusRequest("cancelled"));

        result.Status.Should().Be("cancelled");

        // Verify in DB that sales are unlinked and remain unpaid
        var dbSales = await _dbContext.ConsignmentSales.Where(s => s.ConsignmentSettlementId == settlement.Id).ToListAsync();
        dbSales.Should().BeEmpty(); // Unlinked

        var unpaidSales = await _dbContext.ConsignmentSales.Where(s => s.SupplierId == _supplierId && s.Status == "unpaid").ToListAsync();
        unpaidSales.Should().HaveCount(2); // Retained unpaid status and ready for another settlement attempt
    }
}
