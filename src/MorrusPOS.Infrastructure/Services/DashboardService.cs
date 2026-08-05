using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Dashboard;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _dbContext;

    public DashboardService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(
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

        if (outletId.HasValue)
        {
            query = query.Where(t => t.OutletId == outletId.Value);
        }

        // 1. Basic aggregates
        var basicStats = await query
            .Select(t => new { t.GrandTotal })
            .ToListAsync(ct);

        var totalSales = basicStats.Sum(t => t.GrandTotal);
        var totalTransactions = basicStats.Count;
        var avgOrderValue = totalTransactions > 0 ? totalSales / totalTransactions : 0;

        // 2. Gross profit and margin calculation
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
            .Select(ti => new { ti.Qty, ti.UnitCost })
            .ToListAsync(ct);

        var cogs = itemCosts.Sum(ti => ti.Qty * ti.UnitCost);
        var grossProfit = totalSales - cogs;
        var grossMargin = totalSales > 0 ? (grossProfit / totalSales) * 100 : 0;

        // 3. Sales trend
        // Since we are running in PostgreSQL or InMemory, we can do client-side grouping after fetching keys to avoid SQL translation issues for dates
        var trendData = await query
            .Select(t => new { t.CreatedAt, t.GrandTotal })
            .ToListAsync(ct);

        var salesTrend = trendData
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new SalesTrendItemDto(
                g.Key,
                g.Sum(t => t.GrandTotal),
                g.Count()
            ))
            .OrderBy(x => x.Date)
            .ToList();

        // 4. Payment methods
        var paymentsQuery = _dbContext.Payments
            .AsNoTracking()
            .Where(p => p.Transaction.Status == TransactionStatus.Completed 
                        && p.Transaction.CreatedAt >= startUtc 
                        && p.Transaction.CreatedAt <= endUtc);

        if (outletId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.Transaction.OutletId == outletId.Value);
        }

        var paymentsData = await paymentsQuery
            .Select(p => new { p.Method, p.Amount })
            .ToListAsync(ct);

        var paymentMethods = paymentsData
            .GroupBy(p => p.Method)
            .Select(g => new PaymentMethodDistributionDto(
                g.Key,
                g.Sum(p => p.Amount),
                g.Count()
            ))
            .OrderByDescending(x => x.Amount)
            .ToList();

        // 5. Sales channels
        var channelsData = await query
            .Select(t => new { t.Channel, t.GrandTotal })
            .ToListAsync(ct);

        var salesChannels = channelsData
            .GroupBy(t => t.Channel)
            .Select(g => new ChannelDistributionDto(
                g.Key,
                g.Sum(t => t.GrandTotal),
                g.Count()
            ))
            .OrderByDescending(x => x.Amount)
            .ToList();

        // 6. Top products
        var topProductsQuery = _dbContext.TransactionItems
            .AsNoTracking()
            .Where(ti => ti.Transaction.Status == TransactionStatus.Completed 
                         && ti.Transaction.CreatedAt >= startUtc 
                         && ti.Transaction.CreatedAt <= endUtc);

        if (outletId.HasValue)
        {
            topProductsQuery = topProductsQuery.Where(ti => ti.Transaction.OutletId == outletId.Value);
        }

        var topProductsData = await topProductsQuery
            .Select(ti => new { ti.ProductId, ProductName = ti.Product.Name, Sku = ti.Product.Sku, ti.Qty, ti.LineTotal })
            .ToListAsync(ct);

        var topProducts = topProductsData
            .GroupBy(ti => new { ti.ProductId, ti.ProductName, ti.Sku })
            .Select(g => new TopProductDto(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.Sku,
                g.Sum(ti => ti.Qty),
                g.Sum(ti => ti.LineTotal)
            ))
            .OrderByDescending(x => x.QtySold)
            .Take(5)
            .ToList();

        // 7. Outlet comparisons
        var outletComparisons = new List<OutletSalesComparisonDto>();
        if (!outletId.HasValue)
        {
            var outletData = await query
                .Select(t => new { t.OutletId, OutletName = t.Outlet.Name, t.GrandTotal })
                .ToListAsync(ct);

            outletComparisons = outletData
                .GroupBy(t => new { t.OutletId, t.OutletName })
                .Select(g => new OutletSalesComparisonDto(
                    g.Key.OutletId,
                    g.Key.OutletName,
                    g.Sum(t => t.GrandTotal),
                    g.Count()
                ))
                .OrderByDescending(x => x.TotalSales)
                .ToList();
        }

        return new DashboardSummaryDto(
            totalSales,
            totalTransactions,
            avgOrderValue,
            grossProfit,
            grossMargin,
            salesTrend,
            paymentMethods,
            salesChannels,
            topProducts,
            outletComparisons
        );
    }
}
