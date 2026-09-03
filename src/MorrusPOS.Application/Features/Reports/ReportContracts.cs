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

    Task<SupplierReportDto> GetSupplierReportAsync(
        SupplierReportFilters filters,
        CancellationToken ct = default);

    Task<ExportReportResponse> ExportSupplierReportExcelAsync(
        SupplierReportFilters filters,
        CancellationToken ct = default);

    Task<StockCardReportDto> GetStockCardReportAsync(
        StockCardReportFilters filters,
        CancellationToken ct = default);

    Task<ExportReportResponse> ExportStockCardExcelAsync(
        StockCardReportFilters filters,
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

public record SupplierReportFilters(
    DateTime? DateFrom,
    DateTime? DateTo,
    Guid? SupplierId,
    Guid? OutletId
);

public record SupplierReportSupplierDto(
    Guid SupplierId,
    string SupplierName,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive
);

public record SupplierReportSummaryDto(
    decimal TotalPurchase,
    decimal TotalPurchaseReturn,
    decimal NetPurchase,
    decimal TotalDebtPayment,
    decimal OutstandingDebt,
    decimal ConsignmentReceivedValue,
    decimal ConsignmentReturnValue,
    decimal ConsignmentSalesValue,
    decimal SettlementValue
);

public record SupplierReportPurchaseDto(
    Guid PurchaseOrderId,
    string PoNumber,
    DateTime PoDate,
    Guid OutletId,
    string OutletName,
    string Status,
    string PaymentType,
    decimal TotalAmount,
    DateTime? DueDate
);

public record SupplierReportPurchaseReturnDto(
    Guid ReturnId,
    string ReturnNumber,
    DateTime ReturnDate,
    string Status,
    decimal TotalAmount,
    Guid PurchaseOrderId,
    string PoNumber,
    string? OutletName
);

public record SupplierReportDebtDto(
    Guid DebtId,
    Guid PurchaseOrderId,
    string PoNumber,
    DateTime DueDate,
    decimal Amount,
    decimal PaidAmount,
    decimal RemainingAmount,
    string Status,
    string? OutletName
);

public record SupplierReportPaymentDto(
    Guid PaymentId,
    Guid PurchaseOrderId,
    string PoNumber,
    DateTime PaymentDate,
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber,
    string Status,
    string? OutletName
);

public record SupplierReportConsignmentDto(
    Guid ConsignmentId,
    string ConsignmentNumber,
    DateTime ReceiveDate,
    string Status,
    decimal TotalValue,
    int ItemCount,
    string OutletName
);

public record SupplierReportConsignmentReturnDto(
    Guid ReturnId,
    string ReturnNumber,
    DateTime ReturnDate,
    string Status,
    decimal TotalQty,
    int ItemCount,
    string OutletName
);

public record SupplierReportConsignmentSaleDto(
    Guid ConsignmentSaleId,
    string TransactionNumber,
    DateTime CreatedAt,
    string ProductName,
    decimal Qty,
    decimal UnitCost,
    decimal TotalAmount,
    string Status,
    string OutletName
);

public record SupplierReportSettlementDto(
    Guid SettlementId,
    string SettlementNumber,
    DateTime SettlementDate,
    decimal TotalAmount,
    string Status,
    int SalesCount,
    string OutletName
);

public record SupplierReportDto(
    SupplierReportFilters Filters,
    SupplierReportSupplierDto? Supplier,
    SupplierReportSummaryDto Summary,
    IReadOnlyList<SupplierReportPurchaseDto> Purchases,
    IReadOnlyList<SupplierReportPurchaseReturnDto> PurchaseReturns,
    IReadOnlyList<SupplierReportDebtDto> Debts,
    IReadOnlyList<SupplierReportPaymentDto> Payments,
    IReadOnlyList<SupplierReportConsignmentDto> Consignments,
    IReadOnlyList<SupplierReportConsignmentReturnDto> ConsignmentReturns,
    IReadOnlyList<SupplierReportConsignmentSaleDto> ConsignmentSales,
    IReadOnlyList<SupplierReportSettlementDto> Settlements
);

public record StockCardReportFilters(
    DateTime? DateFrom,
    DateTime? DateTo,
    Guid? OutletId,
    Guid? ProductId,
    Guid? ProductVariantId
);

public record StockCardReportProductInfoDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    Guid? ProductVariantId,
    string? VariantSku,
    string? VariantLabel,
    bool HasVariants
);

public record StockCardReportSummaryDto(
    decimal OpeningBalance,
    decimal TotalIn,
    decimal TotalOut,
    decimal ClosingBalance
);

public record StockCardReportLineDto(
    Guid LedgerId,
    DateTime CreatedAt,
    Guid ProductId,
    string ProductName,
    string Sku,
    Guid? ProductVariantId,
    string? VariantSku,
    string MovementType,
    string MovementLabel,
    string ReferenceType,
    Guid ReferenceId,
    string? ReferenceNumber,
    string? Note,
    decimal QtyChange,
    decimal QtyIn,
    decimal QtyOut,
    decimal RunningBalance,
    string OutletName
);

public record StockCardReportDto(
    StockCardReportFilters Filters,
    StockCardReportProductInfoDto Product,
    string OutletName,
    StockCardReportSummaryDto Summary,
    IReadOnlyList<StockCardReportLineDto> Lines
);

