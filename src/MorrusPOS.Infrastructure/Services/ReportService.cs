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
}
