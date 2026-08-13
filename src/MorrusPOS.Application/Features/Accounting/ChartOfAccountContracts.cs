namespace MorrusPOS.Application.Features.Accounting;

public record ChartOfAccountDto(
    Guid Id,
    Guid BusinessId,
    string AccountCode,
    string AccountName,
    string AccountType,
    bool IsCashBank,
    bool IsActive,
    Guid? OutletId,
    string? OutletName,
    Guid? ParentAccountId,
    string? ParentAccountName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateChartOfAccountRequest(
    string AccountCode,
    string AccountName,
    string AccountType,
    bool IsCashBank,
    Guid? OutletId,
    Guid? ParentAccountId
);

public record UpdateChartOfAccountRequest(
    string AccountCode,
    string AccountName,
    string AccountType,
    bool IsCashBank,
    Guid? OutletId,
    Guid? ParentAccountId,
    bool IsActive
);

public record UpdateChartOfAccountStatusRequest(
    bool IsActive
);

public interface IChartOfAccountService
{
    Task<IReadOnlyList<ChartOfAccountDto>> GetAllAsync(CancellationToken ct = default);
    Task<ChartOfAccountDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ChartOfAccountDto> CreateAsync(CreateChartOfAccountRequest request, CancellationToken ct = default);
    Task<ChartOfAccountDto> UpdateAsync(Guid id, UpdateChartOfAccountRequest request, CancellationToken ct = default);
    Task<ChartOfAccountDto> UpdateStatusAsync(Guid id, UpdateChartOfAccountStatusRequest request, CancellationToken ct = default);
}
