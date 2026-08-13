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
    bool IncludeConsignmentSettlements = true
);

public record AccountingBackfillResultDto(
    int TransactionsPosted,
    int PurchaseOrdersPosted,
    int SupplierPaymentsPosted,
    int SupplierReturnsPosted,
    int ChannelSettlementsPosted,
    int ConsignmentSettlementsPosted
);

public interface IAccountingIntegrationService
{
    Task<bool> EnsureTransactionPostedAsync(Guid transactionId, CancellationToken ct = default);
    Task<bool> EnsurePurchaseOrderPostedAsync(Guid purchaseOrderId, CancellationToken ct = default);
    Task<bool> EnsureSupplierPaymentPostedAsync(Guid supplierPaymentId, CancellationToken ct = default);
    Task<bool> EnsureSupplierReturnPostedAsync(Guid supplierReturnId, CancellationToken ct = default);
    Task<bool> EnsureChannelSettlementPostedAsync(Guid channelSettlementId, CancellationToken ct = default);
    Task<bool> EnsureConsignmentSettlementPostedAsync(Guid consignmentSettlementId, CancellationToken ct = default);
    Task<AccountingPostingStatusDto> GetPostingStatusAsync(string referenceType, Guid referenceId, CancellationToken ct = default);
    Task<AccountingBackfillResultDto> BackfillAsync(AccountingBackfillRequest request, CancellationToken ct = default);
}
