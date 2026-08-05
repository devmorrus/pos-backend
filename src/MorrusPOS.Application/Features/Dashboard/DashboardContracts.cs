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

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(
        Guid? outletId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken ct = default);
}
