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

    public async Task<RoleDashboardDto> GetRoleSummaryAsync(
        string role,
        Guid userId,
        Guid? outletId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken ct = default)
    {
        if (role == "Owner" || role == "Admin")
        {
            var ownerData = await GetSummaryAsync(outletId, startDate, endDate, ct);
            return new RoleDashboardDto(role, OwnerData: ownerData);
        }
        else if (role == "Keuangan")
        {
            var keuanganData = await GetKeuanganDashboardAsync(outletId, startDate, endDate, ct);
            return new RoleDashboardDto(role, KeuanganData: keuanganData);
        }
        else if (role == "Gudang")
        {
            var gudangData = await GetGudangDashboardAsync(outletId, ct);
            return new RoleDashboardDto(role, GudangData: gudangData);
        }
        else // Kasir / KepalaCabang / fallback
        {
            var kasirData = await GetKasirDashboardAsync(userId, outletId, ct);
            return new RoleDashboardDto(role, KasirData: kasirData);
        }
    }

    private async Task<KeuanganDashboardDto> GetKeuanganDashboardAsync(
        Guid? outletId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken ct)
    {
        var startUtc = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(endDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        // 1. Cash on hand estimation (completed cash transactions - completed cash POs)
        var cashIn = await _dbContext.Payments
            .AsNoTracking()
            .Where(p => p.Method == PaymentMethod.Cash && p.Transaction.Status == TransactionStatus.Completed)
            .Where(p => !outletId.HasValue || p.Transaction.OutletId == outletId.Value)
            .SumAsync(p => p.Amount, ct);

        var cashOut = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.PaymentType == PurchaseOrderPaymentType.Cash && po.Status == PurchaseOrderStatus.Completed)
            .Where(po => !outletId.HasValue || po.OutletId == outletId.Value)
            .SumAsync(po => po.TotalAmount, ct);

        var cashOnHand = cashIn - cashOut;

        // 2. Purchases in range
        var poQuery = _dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.Status == PurchaseOrderStatus.Completed && po.PoDate >= startUtc && po.PoDate <= endUtc);

        if (outletId.HasValue)
        {
            poQuery = poQuery.Where(po => po.OutletId == outletId.Value);
        }

        var totalPurchases = await poQuery.SumAsync(po => po.TotalAmount, ct);

        // 3. Supplier Debt Summary
        var debtQuery = _dbContext.SupplierDebts
            .AsNoTracking()
            .Where(d => d.Status != SupplierDebtStatus.Paid);

        if (outletId.HasValue)
        {
            debtQuery = debtQuery.Where(d => d.PurchaseOrder.OutletId == outletId.Value);
        }

        var totalSupplierDebt = await debtQuery.SumAsync(d => d.RemainingAmount, ct);

        // 4. Upcoming Debts
        var upcomingDebts = await debtQuery
            .OrderBy(d => d.DueDate)
            .Take(5)
            .Select(d => new UpcomingDebtDto(
                d.Id,
                d.Supplier.Name,
                d.PurchaseOrder.PoNumber,
                d.DueDate,
                d.RemainingAmount
            ))
            .ToListAsync(ct);

        // 5. Top Suppliers
        var topSuppliers = await poQuery
            .GroupBy(po => new { po.SupplierId, po.Supplier.Name })
            .Select(g => new TopSupplierDto(
                g.Key.SupplierId,
                g.Key.Name,
                g.Sum(po => po.TotalAmount),
                g.Count()
            ))
            .OrderByDescending(s => s.TotalPurchaseAmount)
            .Take(5)
            .ToListAsync(ct);

        // 6. Purchase Trend
        var poTrendData = await poQuery
            .Select(po => new { po.PoDate, po.TotalAmount })
            .ToListAsync(ct);

        var purchaseTrend = poTrendData
            .GroupBy(po => po.PoDate.Date)
            .Select(g => new SalesTrendItemDto(
                g.Key,
                g.Sum(po => po.TotalAmount),
                g.Count()
            ))
            .OrderBy(x => x.Date)
            .ToList();

        return new KeuanganDashboardDto(
            cashOnHand,
            totalPurchases,
            totalSupplierDebt,
            upcomingDebts,
            topSuppliers,
            purchaseTrend
        );
    }

    private async Task<GudangDashboardDto> GetGudangDashboardAsync(Guid? outletId, CancellationToken ct)
    {
        // 1. Total active products
        var totalProducts = await _dbContext.Products.AsNoTracking().CountAsync(ct);

        // 2. Low stock alert count
        var stockQuery = _dbContext.InventoryStocks.AsNoTracking();
        if (outletId.HasValue)
        {
            stockQuery = stockQuery.Where(s => s.OutletId == outletId.Value);
        }

        var lowStockAlertsCount = await stockQuery.CountAsync(s => s.QtyOnHand <= s.MinStockAlert, ct);

        // 3. Pending PO receipts
        var poQuery = _dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.Status == PurchaseOrderStatus.Draft || po.Status == PurchaseOrderStatus.Pending);

        if (outletId.HasValue)
        {
            poQuery = poQuery.Where(po => po.OutletId == outletId.Value);
        }

        var pendingPurchaseOrdersCount = await poQuery.CountAsync(ct);

        // 4. Active consignments
        var consignmentQuery = _dbContext.Consignments
            .AsNoTracking()
            .Where(c => c.Status == ConsignmentStatus.Received);

        if (outletId.HasValue)
        {
            consignmentQuery = consignmentQuery.Where(c => c.OutletId == outletId.Value);
        }

        var activeConsignmentsCount = await consignmentQuery.CountAsync(ct);

        // 5. Pending stock transfers
        var transferQuery = _dbContext.StockTransfers
            .AsNoTracking()
            .Where(t => t.Status == StockTransferStatus.Pending);

        if (outletId.HasValue)
        {
            transferQuery = transferQuery.Where(t => t.FromOutletId == outletId.Value || t.ToOutletId == outletId.Value);
        }

        var pendingStockTransfersCount = await transferQuery.CountAsync(ct);

        // 6. Low stock products list
        var lowStockProducts = await stockQuery
            .Where(s => s.QtyOnHand <= s.MinStockAlert)
            .OrderBy(s => s.QtyOnHand)
            .Take(10)
            .Select(s => new LowStockProductDto(
                s.ProductId,
                s.Product.Name,
                s.Product.Sku,
                s.QtyOnHand,
                s.MinStockAlert
            ))
            .ToListAsync(ct);

        return new GudangDashboardDto(
            totalProducts,
            lowStockAlertsCount,
            pendingPurchaseOrdersCount,
            activeConsignmentsCount,
            pendingStockTransfersCount,
            lowStockProducts
        );
    }

    private async Task<KasirDashboardDto> GetKasirDashboardAsync(Guid userId, Guid? outletId, CancellationToken ct)
    {
        var activeSession = await _dbContext.CashierSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == CashierSessionStatus.Open && (!outletId.HasValue || s.OutletId == outletId.Value), ct);

        if (activeSession == null)
        {
            return new KasirDashboardDto(
                ActiveSession: false,
                SessionId: null,
                OpeningCash: 0,
                TotalSalesThisSession: 0,
                TotalTransactionsThisSession: 0,
                PaymentMethodsThisSession: new List<PaymentMethodDistributionDto>(),
                RecentTransactions: new List<RecentTransactionDto>()
            );
        }

        var sessionTransactionsQuery = _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.CashierSessionId == activeSession.Id && t.Status == TransactionStatus.Completed);

        var sessionTransactions = await sessionTransactionsQuery
            .Select(t => new { t.GrandTotal })
            .ToListAsync(ct);

        var totalSalesThisSession = sessionTransactions.Sum(t => t.GrandTotal);
        var totalTransactionsThisSession = sessionTransactions.Count;

        // Payment Method Distribution
        var paymentsData = await _dbContext.Payments
            .AsNoTracking()
            .Where(p => p.Transaction.CashierSessionId == activeSession.Id && p.Transaction.Status == TransactionStatus.Completed)
            .Select(p => new { p.Method, p.Amount })
            .ToListAsync(ct);

        var paymentMethodsThisSession = paymentsData
            .GroupBy(p => p.Method)
            .Select(g => new PaymentMethodDistributionDto(
                g.Key,
                g.Sum(p => p.Amount),
                g.Count()
            ))
            .OrderByDescending(x => x.Amount)
            .ToList();

        // Recent Transactions
        var recentTransactions = await _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.CashierSessionId == activeSession.Id && t.Status == TransactionStatus.Completed)
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .Select(t => new RecentTransactionDto(
                t.Id,
                t.TransactionNumber,
                t.CreatedAt,
                t.GrandTotal,
                t.Payments.Select(p => p.Method).FirstOrDefault() ?? "cash"
            ))
            .ToListAsync(ct);

        return new KasirDashboardDto(
            ActiveSession: true,
            SessionId: activeSession.Id,
            OpeningCash: activeSession.OpeningCash,
            TotalSalesThisSession: totalSalesThisSession,
            TotalTransactionsThisSession: totalTransactionsThisSession,
            PaymentMethodsThisSession: paymentMethodsThisSession,
            RecentTransactions: recentTransactions
        );
    }
}
