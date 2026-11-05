namespace MorrusPOS.Application.Features.Accounting;

public record JournalDraftLine(
    Guid ChartOfAccountId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Description = null);

public record JournalDraft(
    Guid BusinessId,
    Guid? OutletId,
    DateTime JournalDate,
    string JournalNumber,
    string ReferenceType,
    Guid ReferenceId,
    string Description,
    IReadOnlyList<JournalDraftLine> Lines,
    Guid? ReversalOfJournalId = null,
    Guid? CreatedBy = null);

public record JournalPostingResult(Guid JournalId, bool Created);

public record JournalLineDto(
    Guid Id,
    int LineNumber,
    Guid ChartOfAccountId,
    string AccountCode,
    string AccountName,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Description);

public record JournalEntryDto(
    Guid Id,
    string JournalNumber,
    DateTime JournalDate,
    string ReferenceType,
    Guid ReferenceId,
    string Description,
    string Status,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    string PostingHash,
    Guid? ReversalOfJournalId,
    IReadOnlyList<JournalLineDto> Lines);

public interface IJournalPostingService
{
    Task<JournalPostingResult> PostAsync(JournalDraft draft, CancellationToken ct = default);
    Task<JournalPostingResult> ReverseAsync(
        Guid originalJournalId,
        string referenceType,
        Guid referenceId,
        string journalNumber,
        string description,
        Guid? createdBy,
        CancellationToken ct = default);
    Task<JournalEntryDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<JournalEntryDto?> GetByReferenceAsync(string referenceType, Guid referenceId, CancellationToken ct = default);
}
