using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace MorrusPOS.UnitTests;

public class ProductServiceTests
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserMock;

    public ProductServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _currentUserMock = Substitute.For<ICurrentUserService>();
    }

    [Fact]
    public async Task CreateAsync_Should_CreateProduct_And_SeedZeroStockForAllOutlets()
    {
        // Arrange
        var outletId1 = Guid.NewGuid();
        var outletId2 = Guid.NewGuid();
        _dbContext.Outlets.AddRange(
            new Outlet { Id = outletId1, Code = "OUT-1", Name = "Outlet 1", IsActive = true },
            new Outlet { Id = outletId2, Code = "OUT-2", Name = "Outlet 2", IsActive = true }
        );
        var catId = Guid.NewGuid();
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        await _dbContext.SaveChangesAsync();

        _currentUserMock.UserId.Returns(Guid.NewGuid());
        _currentUserMock.OutletId.Returns(outletId1);

        var service = new ProductService(_dbContext, _currentUserMock);
        var request = new CreateProductRequest(
            CategoryId: catId,
            Sku: "PROD-1",
            Name: "Chiki",
            Barcode: "89901",
            BasePrice: 10000,
            CostPrice: 8000,
            Unit: "pcs",
            IsConsignment: false
        );

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Sku.Should().Be("PROD-1");

        // Verify stock is seeded to both outlets
        var stocks = await _dbContext.InventoryStocks.Where(s => s.ProductId == result.Id).ToListAsync();
        stocks.Should().HaveCount(2);
        stocks.All(s => s.QtyOnHand == 0).Should().BeTrue();

        // Verify audit log is recorded
        var auditLogs = await _dbContext.AuditLogs.Where(l => l.EntityId == result.Id).ToListAsync();
        auditLogs.Should().HaveCount(1);
        auditLogs[0].Action.Should().Be("create");
    }

    [Fact]
    public async Task UpdateAsync_Should_RecordAuditLog_When_PriceChanges()
    {
        // Arrange
        var catId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        var product = new Product
        {
            Id = prodId,
            CategoryId = catId,
            Sku = "PROD-2",
            Name = "Lays",
            BasePrice = 12000,
            CostPrice = 9000,
            Unit = "pcs"
        };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        _currentUserMock.UserId.Returns(Guid.NewGuid());
        _currentUserMock.OutletId.Returns(Guid.NewGuid());

        var service = new ProductService(_dbContext, _currentUserMock);
        var request = new UpdateProductRequest(
            CategoryId: catId,
            Sku: "PROD-2",
            Name: "Lays",
            Barcode: null,
            BasePrice: 13000, // Price updated
            CostPrice: 9500,  // Cost updated
            Unit: "pcs",
            IsConsignment: false,
            IsActive: true
        );

        // Act
        var result = await service.UpdateAsync(prodId, request);

        // Assert
        result.BasePrice.Should().Be(13000);

        // Verify audit log for price_change was created
        var auditLogs = await _dbContext.AuditLogs.Where(l => l.EntityId == prodId && l.Action == "price_change").ToListAsync();
        auditLogs.Should().HaveCount(1);
        auditLogs[0].OldValueJson.Should().Contain("12000");
        auditLogs[0].NewValueJson.Should().Contain("13000");
    }

    [Fact]
    public async Task DeleteAsync_Should_SoftDelete_When_ProductHasSales()
    {
        // Arrange
        var prodId = Guid.NewGuid();
        _dbContext.Products.Add(new Product { Id = prodId, Sku = "PROD-3", Name = "Oreo", Unit = "pcs", IsActive = true });
        _dbContext.TransactionItems.Add(new TransactionItem { Id = Guid.NewGuid(), ProductId = prodId, Qty = 1, UnitPrice = 5000, LineTotal = 5000 });
        await _dbContext.SaveChangesAsync();

        var service = new ProductService(_dbContext, _currentUserMock);

        // Act
        await service.DeleteAsync(prodId);

        // Assert
        var checkProduct = await _dbContext.Products.FindAsync(prodId);
        checkProduct.Should().NotBeNull();
        checkProduct!.IsActive.Should().BeFalse(); // Soft-deleted
    }
}
