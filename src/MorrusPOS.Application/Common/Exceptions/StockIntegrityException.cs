namespace MorrusPOS.Application.Common.Exceptions;

public static class StockIntegrityErrorCodes
{
    public const string LedgerImmutable = "STOCK_LEDGER_IMMUTABLE";
    public const string MovementInvalid = "STOCK_MOVEMENT_INVALID";
    public const string PostingConflict = "STOCK_POSTING_CONFLICT";
    public const string ReconciliationBlocked = "STOCK_RECONCILIATION_BLOCKED";
    public const string NegativeLedgerBalance = "STOCK_LEDGER_NEGATIVE_BALANCE";
    public const string BatchProjectionMismatch = "STOCK_BATCH_PROJECTION_MISMATCH";
    public const string SerialProjectionMismatch = "STOCK_SERIAL_PROJECTION_MISMATCH";
    public const string ReconciliationScopeInvalid = "STOCK_RECONCILIATION_SCOPE_INVALID";
    public const string TransferStatusInvalid = "STOCK_TRANSFER_STATUS_INVALID";
    public const string TransferAlreadyReceived = "STOCK_TRANSFER_ALREADY_RECEIVED";
    public const string TransferItemMismatch = "STOCK_TRANSFER_ITEM_MISMATCH";
    public const string TransferVarianceReasonRequired = "STOCK_TRANSFER_VARIANCE_REASON_REQUIRED";
    public const string TransferVarianceReasonInvalid = "STOCK_TRANSFER_VARIANCE_REASON_INVALID";
    public const string TransferSerialReceiptInvalid = "STOCK_TRANSFER_SERIAL_RECEIPT_INVALID";
}

public sealed class StockIntegrityException : InvalidOperationException
{
    public StockIntegrityException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
