using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MorrusPOS.Application.Features.Reports;

public record ProfitLossReportDto(
    DateTime StartDate,
    DateTime EndDate,
    Guid? OutletId,
    string OutletName,
    decimal GrossRevenue,
    decimal TotalDiscount,
    decimal TotalTax,
    decimal NetRevenue,
    decimal CostOfGoodsSold,
    decimal GrossProfit,
    IReadOnlyList<ProfitLossCategorySummaryDto> CategoryBreakdown
);

public record ProfitLossCategorySummaryDto(
    Guid CategoryId,
    string CategoryName,
    decimal Revenue,
    decimal CostOfGoodsSold,
    decimal GrossProfit
);

// Rekap Pembelian
public record PurchaseRecapReportDto(
    DateTime StartDate,
    DateTime EndDate,
    Guid? OutletId,
    string OutletName,
    decimal TotalSpent,
    int TotalOrdersCount,
    IReadOnlyList<PurchaseProductSummaryDto> ProductBreakdown,
    IReadOnlyList<PurchaseSupplierSummaryDto> SupplierBreakdown
);

public record PurchaseProductSummaryDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal TotalQty,
    decimal AverageUnitCost,
    decimal TotalSpent
);

public record PurchaseSupplierSummaryDto(
    Guid SupplierId,
    string SupplierName,
    int TotalOrders,
    decimal TotalSpent
);

// Rekap Penjualan
public record SalesRecapReportDto(
    DateTime StartDate,
    DateTime EndDate,
    Guid? OutletId,
    string OutletName,
    decimal GrossRevenue,
    decimal TotalDiscount,
    decimal NetRevenue,
    decimal CostOfGoodsSold,
    decimal GrossProfit,
    IReadOnlyList<SalesProductSummaryDto> ProductBreakdown,
    IReadOnlyList<SalesPaymentSummaryDto> PaymentBreakdown
);

public record SalesProductSummaryDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal TotalQty,
    decimal TotalRevenue,
    decimal TotalCostOfGoodsSold,
    decimal TotalGrossProfit
);

public record SalesPaymentSummaryDto(
    string PaymentMethod,
    int TransactionCount,
    decimal TotalCollected
);

public record ExportReportResponse(
    byte[] FileBytes,
    string ContentType,
    string FileName
);

public interface IReportService
{
    Task<ProfitLossReportDto> GetProfitLossReportAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default);

    Task<ExportReportResponse> ExportProfitLossExcelAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default);

    Task<PurchaseRecapReportDto> GetPurchaseRecapReportAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default);

    Task<ExportReportResponse> ExportPurchaseRecapExcelAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default);

    Task<SalesRecapReportDto> GetSalesRecapReportAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default);

    Task<ExportReportResponse> ExportSalesRecapExcelAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default);
}
