using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MorrusPOS.Application.Features.Dashboard;

public record DashboardSummaryDto(
    decimal TotalSales,
    int TotalTransactions,
    decimal AverageOrderValue,
    decimal GrossProfit,
    decimal GrossMargin,
    IReadOnlyList<SalesTrendItemDto> SalesTrend,
    IReadOnlyList<PaymentMethodDistributionDto> PaymentMethods,
    IReadOnlyList<ChannelDistributionDto> SalesChannels,
    IReadOnlyList<TopProductDto> TopProducts,
    IReadOnlyList<OutletSalesComparisonDto> OutletComparisons
);

public record SalesTrendItemDto(
    DateTime Date,
    decimal SalesAmount,
    int TransactionCount
);

public record PaymentMethodDistributionDto(
    string Method,
    decimal Amount,
    int Count
);

public record ChannelDistributionDto(
    string Channel,
    decimal Amount,
    int Count
);

public record TopProductDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal QtySold,
    decimal TotalRevenue
);

public record OutletSalesComparisonDto(
    Guid OutletId,
    string OutletName,
    decimal TotalSales,
    int TotalTransactions
);

public record RoleDashboardDto(
    string Role,
    DashboardSummaryDto? OwnerData = null,
    KeuanganDashboardDto? KeuanganData = null,
    GudangDashboardDto? GudangData = null,
    KasirDashboardDto? KasirData = null
);

public record KeuanganDashboardDto(
    decimal CashOnHand,
    decimal TotalPurchases,
    decimal TotalSupplierDebt,
    IReadOnlyList<UpcomingDebtDto> UpcomingDebts,
    IReadOnlyList<TopSupplierDto> TopSuppliers,
    IReadOnlyList<SalesTrendItemDto> PurchaseTrend
);

public record UpcomingDebtDto(
    Guid SupplierDebtId,
    string SupplierName,
    string PoNumber,
    DateTime DueDate,
    decimal RemainingAmount
);

public record TopSupplierDto(
    Guid SupplierId,
    string SupplierName,
    decimal TotalPurchaseAmount,
    int PoCount
);

public record GudangDashboardDto(
    int TotalProducts,
    int LowStockAlertsCount,
    int PendingPurchaseOrdersCount,
    int ActiveConsignmentsCount,
    int PendingStockTransfersCount,
    IReadOnlyList<LowStockProductDto> LowStockProducts
);

public record LowStockProductDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal QtyOnHand,
    decimal MinStockAlert
);

public record KasirDashboardDto(
    bool ActiveSession,
    Guid? SessionId,
    decimal OpeningCash,
    decimal TotalSalesThisSession,
    int TotalTransactionsThisSession,
    IReadOnlyList<PaymentMethodDistributionDto> PaymentMethodsThisSession,
    IReadOnlyList<RecentTransactionDto> RecentTransactions
);

public record RecentTransactionDto(
    Guid TransactionId,
    string InvoiceNumber,
    DateTime CreatedAt,
    decimal GrandTotal,
    string PaymentMethod
);

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(
        Guid? outletId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken ct = default);

    Task<RoleDashboardDto> GetRoleSummaryAsync(
        string role,
        Guid userId,
        Guid? outletId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken ct = default);
}
