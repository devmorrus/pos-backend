using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Application.Features.Accounting;

public record AccountingAccountMappingDto(
    Guid Id,
    Guid? OutletId,
    string Role,
    Guid ChartOfAccountId,
    string AccountCode,
    string AccountName,
    bool IsActive,
    DateTime UpdatedAt);

public record UpsertAccountingAccountMappingRequest(Guid ChartOfAccountId, Guid? OutletId = null);

public record AccountingMappingIssueDto(string Role, Guid? OutletId, string Reason);

public record AccountingReadinessDto(
    Guid BusinessId,
    bool IsReady,
    IReadOnlyList<string> RequiredMappings,
    IReadOnlyList<string> MissingMappings,
    IReadOnlyList<AccountingMappingIssueDto> InvalidMappings,
    int UnbalancedHistoricalJournals,
    IReadOnlyDictionary<string, int> UnpostedDocumentCounts);

public record AccountingReconciliationRequest(DateTime? DateFrom = null, DateTime? DateTo = null);

public record AccountingReconciliationIssueDto(
    string IssueType,
    string? ReferenceType,
    Guid? ReferenceId,
    Guid? JournalId,
    decimal? ExpectedDebit,
    decimal? ExpectedCredit,
    decimal? ActualDebit,
    decimal? ActualCredit,
    bool Repairable,
    string Message);

public record AccountingReconciliationPreviewDto(
    int IssueCount,
    IReadOnlyList<AccountingReconciliationIssueDto> Issues);

public interface IAccountingAccountMappingService
{
    Task<ChartOfAccount> ResolveAsync(
        Guid businessId,
        Guid? outletId,
        string role,
        CancellationToken ct = default);

    Task<IReadOnlyList<AccountingAccountMappingDto>> GetAsync(Guid? outletId, CancellationToken ct = default);

    Task<AccountingAccountMappingDto> UpsertAsync(
        string role,
        UpsertAccountingAccountMappingRequest request,
        CancellationToken ct = default);
}

public interface IAccountingReconciliationService
{
    Task<AccountingReadinessDto> GetReadinessAsync(CancellationToken ct = default);

    Task<AccountingReconciliationPreviewDto> PreviewAsync(
        AccountingReconciliationRequest request,
        CancellationToken ct = default);
}
