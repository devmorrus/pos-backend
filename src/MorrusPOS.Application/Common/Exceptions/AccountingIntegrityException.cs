namespace MorrusPOS.Application.Common.Exceptions;

public static class AccountingIntegrityErrorCodes
{
    public const string JournalUnbalanced = "JOURNAL_UNBALANCED";
    public const string JournalTooFewLines = "JOURNAL_TOO_FEW_LINES";
    public const string JournalLineInvalid = "JOURNAL_LINE_INVALID";
    public const string JournalPostingConflict = "JOURNAL_POSTING_CONFLICT";
    public const string JournalImmutable = "JOURNAL_IMMUTABLE";
    public const string JournalReferenceDuplicate = "JOURNAL_REFERENCE_DUPLICATE";
    public const string JournalReversalInvalid = "JOURNAL_REVERSAL_INVALID";
    public const string AccountMappingMissing = "ACCOUNT_MAPPING_MISSING";
    public const string AccountMappingInvalid = "ACCOUNT_MAPPING_INVALID";
    public const string AccountingNotReady = "ACCOUNTING_NOT_READY";
    public const string AccountingReconciliationBlocked = "ACCOUNTING_RECONCILIATION_BLOCKED";
}

public sealed class AccountingIntegrityException : InvalidOperationException
{
    public AccountingIntegrityException(string code, string message) : base(message) => Code = code;
    public string Code { get; }
}
