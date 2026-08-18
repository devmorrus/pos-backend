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

public record AccountingCashFlowReportFilters(
    DateTime? DateFrom,
    DateTime? DateTo,
    Guid? OutletId,
    Guid? ChartOfAccountId,
    string? Keyword
);

public record AccountingCashFlowReportSummaryDto(
    decimal OpeningBalance,
    decimal CashIn,
    decimal CashOut,
    decimal ClosingBalance
);

public record AccountingCashFlowReportLineDto(
    Guid AccountTransactionId,
    DateTime TrxDate,
    string TrxNumber,
    string ReferenceType,
    Guid? ReferenceId,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    Guid? OutletId,
    string? OutletName,
    string? Note,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal MovementAmount,
    decimal RunningBalance
);

public record AccountingCashFlowReportDto(
    AccountingCashFlowReportFilters Filters,
    AccountingCashFlowReportSummaryDto Summary,
    IReadOnlyList<AccountingCashFlowReportLineDto> Lines
);

public record AccountingProfitLossReportFilters(
    DateTime? DateFrom,
    DateTime? DateTo,
    Guid? OutletId,
    string? Keyword
);

public record AccountingProfitLossAccountLineDto(
    Guid ChartOfAccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    decimal Amount
);

public record AccountingProfitLossSectionDto(
    string AccountType,
    decimal Total,
    IReadOnlyList<AccountingProfitLossAccountLineDto> Accounts
);

public record AccountingProfitLossReportSummaryDto(
    decimal RevenueTotal,
    decimal CogsTotal,
    decimal ExpenseTotal,
    decimal GrossProfit,
    decimal NetProfit
);

public record AccountingProfitLossReportDto(
    AccountingProfitLossReportFilters Filters,
    AccountingProfitLossSectionDto Revenue,
    AccountingProfitLossSectionDto Cogs,
    AccountingProfitLossSectionDto Expense,
    AccountingProfitLossReportSummaryDto Summary
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
    Task<AccountingCashFlowReportDto> GetCashFlowReportAsync(
        AccountingCashFlowReportFilters filters,
        CancellationToken ct = default);

    Task<ExportReportResponse> ExportCashFlowExcelAsync(
        AccountingCashFlowReportFilters filters,
        CancellationToken ct = default);

    Task<AccountingProfitLossReportDto> GetAccountingProfitLossReportAsync(
        AccountingProfitLossReportFilters filters,
        CancellationToken ct = default);

    Task<ExportReportResponse> ExportAccountingProfitLossExcelAsync(
        AccountingProfitLossReportFilters filters,
        CancellationToken ct = default);

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

    Task<GeneralLedgerReportDto> GetGeneralLedgerReportAsync(
        GeneralLedgerReportFilters filters,
        CancellationToken ct = default);

    Task<ExportReportResponse> ExportGeneralLedgerExcelAsync(
        GeneralLedgerReportFilters filters,
        CancellationToken ct = default);
}

public record GeneralLedgerReportFilters(
    DateTime? DateFrom,
    DateTime? DateTo,
    Guid? OutletId,
    Guid? ChartOfAccountId,
    string? Keyword
);

public record GeneralLedgerReportLineDto(
    Guid AccountTransactionId,
    DateTime TrxDate,
    string TrxNumber,
    string ReferenceType,
    Guid? ReferenceId,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    Guid? OutletId,
    string? OutletName,
    string? Note,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal MovementAmount,
    decimal RunningBalance
);

public record GeneralLedgerReportSummaryDto(
    decimal OpeningBalance,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal ClosingBalance
);

public record GeneralLedgerReportDto(
    GeneralLedgerReportFilters Filters,
    GeneralLedgerReportSummaryDto Summary,
    IReadOnlyList<GeneralLedgerReportLineDto> Lines
);

