using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Reports;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _dbContext;

    public ReportService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProfitLossReportDto> GetProfitLossReportAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        var startUtc = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(endDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        var query = _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= startUtc && t.CreatedAt <= endUtc);

        string outletName = "Semua Outlet";
        if (outletId.HasValue)
        {
            query = query.Where(t => t.OutletId == outletId.Value);
            var outlet = await _dbContext.Outlets.FindAsync(new object[] { outletId.Value }, ct);
            if (outlet != null)
            {
                outletName = outlet.Name;
            }
        }

        var transactions = await query
            .Select(t => new { t.Subtotal, t.DiscountTotal, t.TaxTotal, t.GrandTotal })
            .ToListAsync(ct);

        decimal grossRevenue = transactions.Sum(t => t.Subtotal);
        decimal totalDiscount = transactions.Sum(t => t.DiscountTotal);
        decimal totalTax = transactions.Sum(t => t.TaxTotal);
        decimal netRevenue = transactions.Sum(t => t.GrandTotal);

        var itemCostsQuery = _dbContext.TransactionItems
            .AsNoTracking()
            .Where(ti => ti.Transaction.Status == TransactionStatus.Completed 
                         && ti.Transaction.CreatedAt >= startUtc 
                         && ti.Transaction.CreatedAt <= endUtc);

        if (outletId.HasValue)
        {
            itemCostsQuery = itemCostsQuery.Where(ti => ti.Transaction.OutletId == outletId.Value);
        }

        var itemCosts = await itemCostsQuery
            .Select(ti => new {
                CategoryId = ti.Product.CategoryId,
                CategoryName = ti.Product.Category != null ? ti.Product.Category.Name : "Tanpa Kategori",
                Revenue = ti.LineTotal,
                Cost = ti.Qty * ti.UnitCost
            })
            .ToListAsync(ct);

        decimal costOfGoodsSold = itemCosts.Sum(ti => ti.Cost);
        decimal grossProfit = netRevenue - costOfGoodsSold;

        var categoryBreakdown = itemCosts
            .GroupBy(x => new { x.CategoryId, x.CategoryName })
            .Select(g => new ProfitLossCategorySummaryDto(
                g.Key.CategoryId,
                g.Key.CategoryName,
                g.Sum(x => x.Revenue),
                g.Sum(x => x.Cost),
                g.Sum(x => x.Revenue - x.Cost)
            ))
            .OrderByDescending(x => x.Revenue)
            .ToList();

        return new ProfitLossReportDto(
            startDate,
            endDate,
            outletId,
            outletName,
            grossRevenue,
            totalDiscount,
            totalTax,
            netRevenue,
            costOfGoodsSold,
            grossProfit,
            categoryBreakdown
        );
    }

    public async Task<ExportReportResponse> ExportProfitLossExcelAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        var report = await GetProfitLossReportAsync(outletId, startDate, endDate, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Laporan Laba Rugi MorrusPOS");
        sb.AppendLine($"Periode:;{report.StartDate:yyyy-MM-dd} s/d {report.EndDate:yyyy-MM-dd}");
        sb.AppendLine($"Outlet:;{report.OutletName}");
        sb.AppendLine();

        sb.AppendLine("RINGKASAN FINANSIAL");
        sb.AppendLine("Metrik;Nilai");
        sb.AppendLine($"Pendapatan Kotor (Gross Revenue);{report.GrossRevenue:F2}");
        sb.AppendLine($"Total Diskon;{report.TotalDiscount:F2}");
        sb.AppendLine($"Total Pajak;{report.TotalTax:F2}");
        sb.AppendLine($"Pendapatan Bersih (Net Revenue);{report.NetRevenue:F2}");
        sb.AppendLine($"Harga Pokok Penjualan (HPP / COGS);{report.CostOfGoodsSold:F2}");
        sb.AppendLine($"Laba Kotor (Gross Profit);{report.GrossProfit:F2}");
        sb.AppendLine();

        sb.AppendLine("RINCIAN PER KATEGORI");
        sb.AppendLine("Kategori;Pendapatan;HPP;Laba Kotor");
        foreach (var cat in report.CategoryBreakdown)
        {
            sb.AppendLine($"{cat.CategoryName};{cat.Revenue:F2};{cat.CostOfGoodsSold:F2};{cat.GrossProfit:F2}");
        }

        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"Laporan_Laba_Rugi_{report.StartDate:yyyyMMdd}_{report.EndDate:yyyyMMdd}.csv";

        return new ExportReportResponse(csvBytes, "text/csv", fileName);
    }

    public async Task<PurchaseRecapReportDto> GetPurchaseRecapReportAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        var startUtc = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(endDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        var poQuery = _dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(p => p.Status == "completed" && p.CreatedAt >= startUtc && p.CreatedAt <= endUtc);

        string outletName = "Semua Outlet";
        if (outletId.HasValue)
        {
            poQuery = poQuery.Where(p => p.OutletId == outletId.Value);
            var outlet = await _dbContext.Outlets.FindAsync(new object[] { outletId.Value }, ct);
            if (outlet != null)
            {
                outletName = outlet.Name;
            }
        }

        var pos = await poQuery
            .Select(p => new { p.Id, p.TotalAmount, p.SupplierId, SupplierName = p.Supplier.Name })
            .ToListAsync(ct);

        decimal totalSpent = pos.Sum(p => p.TotalAmount);
        int totalOrdersCount = pos.Count;

        var poIds = pos.Select(p => p.Id).ToList();

        var itemsQuery = _dbContext.PurchaseOrderItems
            .AsNoTracking()
            .Where(pi => poIds.Contains(pi.PurchaseOrderId));

        var items = await itemsQuery
            .Select(pi => new {
                pi.ProductId,
                ProductName = pi.Product.Name,
                ProductSku = pi.Product.Sku,
                pi.Qty,
                pi.UnitCost,
                LineTotal = pi.Qty * pi.UnitCost
            })
            .ToListAsync(ct);

        var productBreakdown = items
            .GroupBy(x => new { x.ProductId, x.ProductName, x.ProductSku })
            .Select(g => {
                var totalQty = g.Sum(x => x.Qty);
                var totalProductSpent = g.Sum(x => x.LineTotal);
                var avgUnitCost = totalQty > 0 ? totalProductSpent / totalQty : 0;
                return new PurchaseProductSummaryDto(
                    g.Key.ProductId,
                    g.Key.ProductName,
                    g.Key.ProductSku,
                    totalQty,
                    avgUnitCost,
                    totalProductSpent
                );
            })
            .OrderByDescending(x => x.TotalSpent)
            .ToList();

        var supplierBreakdown = pos
            .GroupBy(x => new { x.SupplierId, x.SupplierName })
            .Select(g => new PurchaseSupplierSummaryDto(
                g.Key.SupplierId,
                g.Key.SupplierName,
                g.Count(),
                g.Sum(x => x.TotalAmount)
            ))
            .OrderByDescending(x => x.TotalSpent)
            .ToList();

        return new PurchaseRecapReportDto(
            startDate,
            endDate,
            outletId,
            outletName,
            totalSpent,
            totalOrdersCount,
            productBreakdown,
            supplierBreakdown
        );
    }

    public async Task<ExportReportResponse> ExportPurchaseRecapExcelAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        var report = await GetPurchaseRecapReportAsync(outletId, startDate, endDate, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Laporan Rekap Pembelian MorrusPOS");
        sb.AppendLine($"Periode:;{report.StartDate:yyyy-MM-dd} s/d {report.EndDate:yyyy-MM-dd}");
        sb.AppendLine($"Outlet:;{report.OutletName}");
        sb.AppendLine();

        sb.AppendLine("RINGKASAN PEMBELIAN");
        sb.AppendLine("Metrik;Nilai");
        sb.AppendLine($"Total Pengeluaran Belanja;{report.TotalSpent:F2}");
        sb.AppendLine($"Total Dokumen PO Selesai;{report.TotalOrdersCount}");
        sb.AppendLine();

        sb.AppendLine("RINCIAN PEMBELIAN PER PRODUK");
        sb.AppendLine("SKU;Nama Produk;Total Qty Belanja;Harga Rata-Rata Beli;Total Belanja");
        foreach (var p in report.ProductBreakdown)
        {
            sb.AppendLine($"{p.Sku};{p.ProductName};{p.TotalQty:F2};{p.AverageUnitCost:F2};{p.TotalSpent:F2}");
        }
        sb.AppendLine();

        sb.AppendLine("RINCIAN BELANJA PER SUPPLIER");
        sb.AppendLine("Nama Supplier;Total Dokumen PO;Total Belanja");
        foreach (var s in report.SupplierBreakdown)
        {
            sb.AppendLine($"{s.SupplierName};{s.TotalOrders};{s.TotalSpent:F2}");
        }

        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"Rekap_Pembelian_{report.StartDate:yyyyMMdd}_{report.EndDate:yyyyMMdd}.csv";

        return new ExportReportResponse(csvBytes, "text/csv", fileName);
    }

    public async Task<SalesRecapReportDto> GetSalesRecapReportAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        var startUtc = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(endDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        var txQuery = _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= startUtc && t.CreatedAt <= endUtc);

        string outletName = "Semua Outlet";
        if (outletId.HasValue)
        {
            txQuery = txQuery.Where(t => t.OutletId == outletId.Value);
            var outlet = await _dbContext.Outlets.FindAsync(new object[] { outletId.Value }, ct);
            if (outlet != null)
            {
                outletName = outlet.Name;
            }
        }

        var transactions = await txQuery
            .Select(t => new { t.Id, t.Subtotal, t.DiscountTotal, t.TaxTotal, t.GrandTotal })
            .ToListAsync(ct);

        decimal grossRevenue = transactions.Sum(t => t.Subtotal);
        decimal totalDiscount = transactions.Sum(t => t.DiscountTotal);
        decimal totalTax = transactions.Sum(t => t.TaxTotal);
        decimal netRevenue = transactions.Sum(t => t.GrandTotal);

        var txIds = transactions.Select(t => t.Id).ToList();

        var itemsQuery = _dbContext.TransactionItems
            .AsNoTracking()
            .Where(ti => txIds.Contains(ti.TransactionId));

        var items = await itemsQuery
            .Select(ti => new {
                ti.ProductId,
                ProductName = ti.Product.Name,
                ProductSku = ti.Product.Sku,
                ti.Qty,
                ti.UnitPrice,
                ti.LineTotal,
                ti.UnitCost
            })
            .ToListAsync(ct);

        var productBreakdown = items
            .GroupBy(x => new { x.ProductId, x.ProductName, x.ProductSku })
            .Select(g => {
                var totalQty = g.Sum(x => x.Qty);
                var totalRevenue = g.Sum(x => x.LineTotal);
                var totalCost = g.Sum(x => x.Qty * x.UnitCost);
                var totalProfit = totalRevenue - totalCost;
                return new SalesProductSummaryDto(
                    g.Key.ProductId,
                    g.Key.ProductName,
                    g.Key.ProductSku,
                    totalQty,
                    totalRevenue,
                    totalCost,
                    totalProfit
                );
            })
            .OrderByDescending(x => x.TotalRevenue)
            .ToList();

        decimal costOfGoodsSold = productBreakdown.Sum(x => x.TotalCostOfGoodsSold);
        decimal grossProfit = netRevenue - costOfGoodsSold;

        var paymentsQuery = _dbContext.Payments
            .AsNoTracking()
            .Where(p => txIds.Contains(p.TransactionId));

        var payments = await paymentsQuery
            .Select(p => new { PaymentMethod = p.Method, p.Amount })
            .ToListAsync(ct);

        var paymentBreakdown = payments
            .GroupBy(x => x.PaymentMethod)
            .Select(g => new SalesPaymentSummaryDto(
                string.IsNullOrWhiteSpace(g.Key) ? "Lainnya" : g.Key,
                g.Count(),
                g.Sum(x => x.Amount)
            ))
            .OrderByDescending(x => x.TotalCollected)
            .ToList();

        return new SalesRecapReportDto(
            startDate,
            endDate,
            outletId,
            outletName,
            grossRevenue,
            totalDiscount,
            netRevenue,
            costOfGoodsSold,
            grossProfit,
            productBreakdown,
            paymentBreakdown
        );
    }

    public async Task<ExportReportResponse> ExportSalesRecapExcelAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        var report = await GetSalesRecapReportAsync(outletId, startDate, endDate, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Laporan Rekap Penjualan MorrusPOS");
        sb.AppendLine($"Periode:;{report.StartDate:yyyy-MM-dd} s/d {report.EndDate:yyyy-MM-dd}");
        sb.AppendLine($"Outlet:;{report.OutletName}");
        sb.AppendLine();

        sb.AppendLine("RINGKASAN PENJUALAN");
        sb.AppendLine("Metrik;Nilai");
        sb.AppendLine($"Pendapatan Kotor (Gross Revenue);{report.GrossRevenue:F2}");
        sb.AppendLine($"Total Diskon;{report.TotalDiscount:F2}");
        sb.AppendLine($"Pendapatan Bersih (Net Revenue);{report.NetRevenue:F2}");
        sb.AppendLine($"Harga Pokok Penjualan (HPP / COGS);{report.CostOfGoodsSold:F2}");
        sb.AppendLine($"Laba Kotor (Gross Profit);{report.GrossProfit:F2}");
        sb.AppendLine();

        sb.AppendLine("RINCIAN PENJUALAN PER PRODUK");
        sb.AppendLine("SKU;Nama Produk;Qty Terjual;Total Omzet;Total HPP;Total Laba");
        foreach (var p in report.ProductBreakdown)
        {
            sb.AppendLine($"{p.Sku};{p.ProductName};{p.TotalQty:F2};{p.TotalRevenue:F2};{p.TotalCostOfGoodsSold:F2};{p.TotalGrossProfit:F2}");
        }
        sb.AppendLine();

        sb.AppendLine("RINCIAN PENERIMAAN PER METODE PEMBAYARAN");
        sb.AppendLine("Metode Pembayaran;Total Transaksi;Total Diterima");
        foreach (var pay in report.PaymentBreakdown)
        {
            sb.AppendLine($"{pay.PaymentMethod};{pay.TransactionCount};{pay.TotalCollected:F2}");
        }

        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"Rekap_Penjualan_{report.StartDate:yyyyMMdd}_{report.EndDate:yyyyMMdd}.csv";

        return new ExportReportResponse(csvBytes, "text/csv", fileName);
    }
}
