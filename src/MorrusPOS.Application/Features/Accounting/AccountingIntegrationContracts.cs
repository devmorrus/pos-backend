namespace MorrusPOS.Application.Features.Accounting;

public record AccountingPostingStatusDto(
    string ReferenceType,
    Guid ReferenceId,
    bool IsPosted,
    int EntryCount,
    string? TrxNumber,
    DateTime? TrxDate
);

public record AccountingBackfillRequest(
    DateTime? DateFrom,
    DateTime? DateTo,
    bool IncludeTransactions = true,
    bool IncludePurchaseOrders = true,
    bool IncludeSupplierPayments = true,
    bool IncludeSupplierReturns = true,
    bool IncludeChannelSettlements = true,
    bool IncludeConsignmentSettlements = true,
    bool IncludeReceivingNotes = true,
    bool IncludeRefunds = true,
    bool IncludeVoids = true,
    bool IncludePettyCash = true,
    bool IncludeStockOpnames = true,
    bool IncludeCashFlows = true
);

public record AccountingBackfillResultDto(
    int TransactionsPosted,
    int PurchaseOrdersPosted,
    int SupplierPaymentsPosted,
    int SupplierReturnsPosted,
    int ChannelSettlementsPosted,
    int ConsignmentSettlementsPosted,
    int ReceivingNotesPosted = 0,
    int RefundsPosted = 0,
    int VoidsPosted = 0,
    int PettyCashPosted = 0,
    int StockOpnamesPosted = 0,
    int CashFlowsPosted = 0,
    int Scanned = 0,
    int Posted = 0,
    int AlreadyPosted = 0,
    int SkippedNotApplicable = 0,
    int BlockedMissingMapping = 0,
    int BlockedInvalidSource = 0,
    int Failed = 0
);

public interface IAccountingIntegrationService
{
    Task<bool> EnsureTransactionPostedAsync(Guid transactionId, CancellationToken ct = default);
    Task<bool> EnsurePurchaseOrderPostedAsync(Guid purchaseOrderId, CancellationToken ct = default);
    Task<bool> EnsureReceivingNotePostedAsync(Guid receivingNoteId, CancellationToken ct = default);
    Task<bool> EnsureSupplierPaymentPostedAsync(Guid supplierPaymentId, CancellationToken ct = default);
    Task<bool> EnsureSupplierReturnPostedAsync(Guid supplierReturnId, CancellationToken ct = default);
    Task<bool> EnsureChannelSettlementPostedAsync(Guid channelSettlementId, CancellationToken ct = default);
    Task<bool> EnsureConsignmentSettlementPostedAsync(Guid consignmentSettlementId, CancellationToken ct = default);
    Task<bool> EnsureRefundPostedAsync(Guid transactionId, Guid refundRequestId, CancellationToken ct = default);
    Task<bool> EnsureVoidPostedAsync(Guid transactionId, CancellationToken ct = default);
    Task<bool> EnsurePettyCashPostedAsync(Guid pettyCashExpenseId, CancellationToken ct = default);
    Task<bool> EnsureStockOpnamePostedAsync(Guid stockOpnameId, CancellationToken ct = default);
    Task<bool> EnsureCashFlowPostedAsync(Guid cashFlowId, CancellationToken ct = default);
    Task<AccountingPostingStatusDto> GetPostingStatusAsync(string referenceType, string referenceIdentifier, CancellationToken ct = default);
    Task<AccountingBackfillResultDto> BackfillAsync(AccountingBackfillRequest request, CancellationToken ct = default);
}
