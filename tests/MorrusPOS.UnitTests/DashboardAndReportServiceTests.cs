using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Dashboard;
using MorrusPOS.Application.Features.Reports;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using Xunit;

namespace MorrusPOS.UnitTests;

public class DashboardAndReportServiceTests : IDisposable
{
    private readonly AppDbContext _dbContext;

    public DashboardAndReportServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetSummaryAsync_Should_CalculateCorrectAggregates()
    {
        // Arrange
        var outletId = Guid.NewGuid();
        var outlet = new Outlet { Id = outletId, Code = "OUT-1", Name = "Outlet 1", IsActive = true };
        _dbContext.Outlets.Add(outlet);

        var category = new Category { Id = Guid.NewGuid(), Name = "Makanan" };
        _dbContext.Categories.Add(category);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Sku = "SKU-001",
            Name = "Beras 5kg",
            BasePrice = 60000,
            CostPrice = 50000,
            Unit = "pcs",
            IsActive = true
        };
        _dbContext.Products.Add(product);

        var trxId = Guid.NewGuid();
        var transaction = new Transaction
        {
            Id = trxId,
            OutletId = outletId,
            UserId = Guid.NewGuid(),
            TransactionNumber = "TRX-1",
            Channel = TransactionChannel.Pos,
            Status = TransactionStatus.Completed,
            Subtotal = 60000,
            DiscountTotal = 5000,
            TaxTotal = 0,
            GrandTotal = 55000,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Transactions.Add(transaction);

        var item = new TransactionItem
        {
            Id = Guid.NewGuid(),
            TransactionId = trxId,
            ProductId = product.Id,
            Qty = 1,
            UnitPrice = 60000,
            UnitCost = 50000,
            DiscountAmount = 5000,
            LineTotal = 55000
        };
        _dbContext.TransactionItems.Add(item);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            TransactionId = trxId,
            Method = PaymentMethod.Cash,
            Amount = 55000
        };
        _dbContext.Payments.Add(payment);

        await _dbContext.SaveChangesAsync();

        var service = new DashboardService(_dbContext);

        // Act
        var result = await service.GetSummaryAsync(outletId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        // Assert
        result.TotalSales.Should().Be(55000);
        result.TotalTransactions.Should().Be(1);
        result.AverageOrderValue.Should().Be(55000);
        result.GrossProfit.Should().Be(5000); // 55000 (GrandTotal) - 50000 (HPP)
        ((double)result.GrossMargin).Should().BeApproximately(9.09, 0.01);
        result.SalesTrend.Should().HaveCount(1);
        result.PaymentMethods.Should().HaveCount(1);
        result.PaymentMethods[0].Method.Should().Be("cash");
        result.PaymentMethods[0].Amount.Should().Be(55000);
        result.TopProducts.Should().HaveCount(1);
        result.TopProducts[0].ProductName.Should().Be("Beras 5kg");
        result.TopProducts[0].QtySold.Should().Be(1);
    }

    [Fact]
    public async Task GetProfitLossReportAsync_Should_CalculateCorrectProfitLoss()
    {
        // Arrange
        var outletId = Guid.NewGuid();
        var outlet = new Outlet { Id = outletId, Code = "OUT-1", Name = "Outlet 1", IsActive = true };
        _dbContext.Outlets.Add(outlet);

        var category = new Category { Id = Guid.NewGuid(), Name = "Sembako" };
        _dbContext.Categories.Add(category);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Sku = "SKU-003",
            Name = "Beras 5kg",
            BasePrice = 65000,
            CostPrice = 50000,
            Unit = "pcs",
            IsActive = true
        };
        _dbContext.Products.Add(product);

        var trxId = Guid.NewGuid();
        var transaction = new Transaction
        {
            Id = trxId,
            OutletId = outletId,
            UserId = Guid.NewGuid(),
            TransactionNumber = "TRX-2",
            Channel = TransactionChannel.Pos,
            Status = TransactionStatus.Completed,
            Subtotal = 65000,
            DiscountTotal = 0,
            TaxTotal = 0,
            GrandTotal = 65000,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Transactions.Add(transaction);

        var item = new TransactionItem
        {
            Id = Guid.NewGuid(),
            TransactionId = trxId,
            ProductId = product.Id,
            Qty = 1,
            UnitPrice = 65000,
            UnitCost = 50000,
            DiscountAmount = 0,
            LineTotal = 65000
        };
        _dbContext.TransactionItems.Add(item);

        await _dbContext.SaveChangesAsync();

        var service = new ReportService(_dbContext);

        // Act
        var report = await service.GetProfitLossReportAsync(outletId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        var export = await service.ExportProfitLossExcelAsync(outletId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        // Assert
        report.GrossRevenue.Should().Be(65000);
        report.CostOfGoodsSold.Should().Be(50000);
        report.GrossProfit.Should().Be(15000);
        report.CategoryBreakdown.Should().HaveCount(1);
        report.CategoryBreakdown[0].CategoryName.Should().Be("Sembako");
        report.CategoryBreakdown[0].GrossProfit.Should().Be(15000);

        export.FileBytes.Should().NotBeNull();
        export.ContentType.Should().Be("text/csv");
        export.FileName.Should().Contain("Laporan_Laba_Rugi");
    }

    [Fact]
    public async Task GetPurchaseRecapReportAsync_Should_CalculateCorrectPurchases()
    {
        // Arrange
        var supplierId = Guid.NewGuid();
        var supplier = new Supplier { Id = supplierId, Name = "Supplier Utama", ContactPerson = "Budi" };
        _dbContext.Suppliers.Add(supplier);

        var outletId = Guid.NewGuid();
        var outlet = new Outlet { Id = outletId, Code = "OUT-1", Name = "Outlet Utama", IsActive = true };
        _dbContext.Outlets.Add(outlet);

        var category = new Category { Id = Guid.NewGuid(), Name = "Sembako" };
        _dbContext.Categories.Add(category);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Sku = "SKU-999",
            Name = "Minyak Goreng 1L",
            BasePrice = 20000,
            CostPrice = 15000,
            Unit = "pcs",
            IsActive = true
        };
        _dbContext.Products.Add(product);

        var poId = Guid.NewGuid();
        var po = new PurchaseOrder
        {
            Id = poId,
            SupplierId = supplierId,
            OutletId = outletId,
            PoNumber = "PO-2026-001",
            PoDate = DateTime.UtcNow,
            Status = "completed",
            TotalAmount = 75000,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.PurchaseOrders.Add(po);

        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = poId,
            ProductId = product.Id,
            Qty = 5,
            UnitCost = 15000
        };
        _dbContext.PurchaseOrderItems.Add(poItem);

        await _dbContext.SaveChangesAsync();

        var service = new ReportService(_dbContext);

        // Act
        var report = await service.GetPurchaseRecapReportAsync(outletId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        var export = await service.ExportPurchaseRecapExcelAsync(outletId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        // Assert
        report.TotalSpent.Should().Be(75000);
        report.TotalOrdersCount.Should().Be(1);
        report.ProductBreakdown.Should().HaveCount(1);
        report.ProductBreakdown[0].ProductName.Should().Be("Minyak Goreng 1L");
        report.ProductBreakdown[0].TotalQty.Should().Be(5);
        report.ProductBreakdown[0].AverageUnitCost.Should().Be(15000);
        report.ProductBreakdown[0].TotalSpent.Should().Be(75000);

        report.SupplierBreakdown.Should().HaveCount(1);
        report.SupplierBreakdown[0].SupplierName.Should().Be("Supplier Utama");
        report.SupplierBreakdown[0].TotalOrders.Should().Be(1);
        report.SupplierBreakdown[0].TotalSpent.Should().Be(75000);

        export.FileBytes.Should().NotBeNull();
        export.ContentType.Should().Be("text/csv");
        export.FileName.Should().Contain("Rekap_Pembelian");
    }

    [Fact]
    public async Task GetSalesRecapReportAsync_Should_CalculateCorrectSales()
    {
        // Arrange
        var outletId = Guid.NewGuid();
        var outlet = new Outlet { Id = outletId, Code = "OUT-1", Name = "Outlet Utama", IsActive = true };
        _dbContext.Outlets.Add(outlet);

        var category = new Category { Id = Guid.NewGuid(), Name = "Minuman" };
        _dbContext.Categories.Add(category);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Sku = "SKU-888",
            Name = "Teh Manis",
            BasePrice = 5000,
            CostPrice = 3000,
            Unit = "pcs",
            IsActive = true
        };
        _dbContext.Products.Add(product);

        var trxId = Guid.NewGuid();
        var transaction = new Transaction
        {
            Id = trxId,
            OutletId = outletId,
            UserId = Guid.NewGuid(),
            TransactionNumber = "TRX-SALES-RECAP",
            Channel = TransactionChannel.Pos,
            Status = TransactionStatus.Completed,
            Subtotal = 10000,
            DiscountTotal = 1000,
            TaxTotal = 0,
            GrandTotal = 9000,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Transactions.Add(transaction);

        var item = new TransactionItem
        {
            Id = Guid.NewGuid(),
            TransactionId = trxId,
            ProductId = product.Id,
            Qty = 2,
            UnitPrice = 5000,
            UnitCost = 3000,
            DiscountAmount = 1000,
            LineTotal = 9000
        };
        _dbContext.TransactionItems.Add(item);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            TransactionId = trxId,
            Method = "qris",
            Amount = 9000
        };
        _dbContext.Payments.Add(payment);

        await _dbContext.SaveChangesAsync();

        var service = new ReportService(_dbContext);

        // Act
        var report = await service.GetSalesRecapReportAsync(outletId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        var export = await service.ExportSalesRecapExcelAsync(outletId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        // Assert
        report.GrossRevenue.Should().Be(10000);
        report.TotalDiscount.Should().Be(1000);
        report.NetRevenue.Should().Be(9000);
        report.CostOfGoodsSold.Should().Be(6000); // 2 * 3000
        report.GrossProfit.Should().Be(3000); // 9000 - 6000

        report.ProductBreakdown.Should().HaveCount(1);
        report.ProductBreakdown[0].ProductName.Should().Be("Teh Manis");
        report.ProductBreakdown[0].TotalQty.Should().Be(2);
        report.ProductBreakdown[0].TotalRevenue.Should().Be(9000);
        report.ProductBreakdown[0].TotalCostOfGoodsSold.Should().Be(6000);
        report.ProductBreakdown[0].TotalGrossProfit.Should().Be(3000);

        report.PaymentBreakdown.Should().HaveCount(1);
        report.PaymentBreakdown[0].PaymentMethod.Should().Be("qris");
        report.PaymentBreakdown[0].TransactionCount.Should().Be(1);
        report.PaymentBreakdown[0].TotalCollected.Should().Be(9000);

        export.FileBytes.Should().NotBeNull();
        export.ContentType.Should().Be("text/csv");
        export.FileName.Should().Contain("Rekap_Penjualan");
    }
}
