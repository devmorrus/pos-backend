namespace MorrusPOS.Application.Features.Accounting;

public record CashFlowListItemDto(
    Guid Id,
    string TrxNumber,
    DateTime TrxDate,
    string TrxType,
    string TrxEntity,
    decimal Amount,
    Guid FromChartOfAccountId,
    string FromChartOfAccountCode,
    string FromChartOfAccountName,
    Guid ToChartOfAccountId,
    string ToChartOfAccountCode,
    string ToChartOfAccountName,
    Guid? OutletId,
    string? OutletName,
    string? Note,
    string? AttachmentUrl,
    Guid CreatedBy,
    string CreatedByName,
    DateTime CreatedAt
);

public record CashFlowJournalEntryDto(
    Guid AccountTransactionId,
    Guid ChartOfAccountId,
    string AccountCode,
    string AccountName,
    decimal DebitAmount,
    decimal CreditAmount
);

public record CashFlowDetailDto(
    Guid Id,
    string TrxNumber,
    DateTime TrxDate,
    string TrxType,
    string TrxEntity,
    decimal Amount,
    Guid FromChartOfAccountId,
    string FromChartOfAccountCode,
    string FromChartOfAccountName,
    Guid ToChartOfAccountId,
    string ToChartOfAccountCode,
    string ToChartOfAccountName,
    Guid? OutletId,
    string? OutletName,
    string? Note,
    string? AttachmentUrl,
    Guid CreatedBy,
    string CreatedByName,
    DateTime CreatedAt,
    IReadOnlyList<CashFlowJournalEntryDto> JournalEntries
);

public record CreateBusinessIncomeRequest(
    DateTime TrxDate,
    Guid? OutletId,
    Guid FromChartOfAccountId,
    Guid ToChartOfAccountId,
    decimal Amount,
    string? Note,
    string? AttachmentUrl
);

public record CreateBusinessOutcomeRequest(
    DateTime TrxDate,
    Guid? OutletId,
    Guid FromChartOfAccountId,
    Guid ToChartOfAccountId,
    decimal Amount,
    string? Note,
    string? AttachmentUrl
);

public record CashFlowFilters(
    string? TrxType,
    Guid? OutletId,
    DateTime? DateFrom,
    DateTime? DateTo,
    Guid? ChartOfAccountId,
    string? Keyword
);

public interface ICashFlowService
{
    Task<IReadOnlyList<CashFlowListItemDto>> GetAsync(CashFlowFilters filters, CancellationToken ct = default);
    Task<CashFlowDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CashFlowDetailDto> CreateIncomeAsync(CreateBusinessIncomeRequest request, CancellationToken ct = default);
    Task<CashFlowDetailDto> CreateOutcomeAsync(CreateBusinessOutcomeRequest request, CancellationToken ct = default);
}

public interface ICashFlowPostingService
{
    Task<IReadOnlyList<CashFlowJournalEntryDto>> PostAsync(
        Domain.Entities.CashFlow cashFlow,
        Domain.Entities.ChartOfAccount fromAccount,
        Domain.Entities.ChartOfAccount toAccount,
        CancellationToken ct = default);
}
