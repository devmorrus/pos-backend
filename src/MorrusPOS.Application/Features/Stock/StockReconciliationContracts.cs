namespace MorrusPOS.Application.Features.Stock;

public record PreviewStockReconciliationRequest(Guid OutletId);
public record RepairStockReconciliationRequest(Guid OutletId, string Reason);

public record StockReconciliationMismatchDto(
    Guid ProductId,
    Guid? ProductVariantId,
    string Sku,
    string ProductName,
    string TrackingMode,
    decimal LedgerQty,
    decimal StoredQty,
    decimal Difference,
    decimal BatchQty,
    int AvailableSerialCount,
    string IssueType,
    bool Repairable,
    string Action);

public record StockReconciliationResultDto(
    Guid? RunId,
    Guid OutletId,
    string Mode,
    string Status,
    int ScannedCount,
    int MismatchCount,
    int RepairableCount,
    int RepairedCount,
    int BlockedCount,
    DateTime StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<StockReconciliationMismatchDto> Items);

public record StockReconciliationRunDto(
    Guid Id,
    Guid OutletId,
    string OutletName,
    Guid RequestedBy,
    string RequestedByName,
    string Mode,
    string Status,
    string? Reason,
    int ScannedCount,
    int MismatchCount,
    int RepairableCount,
    int RepairedCount,
    int BlockedCount,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime StartedAt,
    DateTime? CompletedAt);

public interface IStockReconciliationService
{
    Task<StockReconciliationResultDto> PreviewAsync(Guid userId, Guid outletId, CancellationToken ct = default);
    Task<StockReconciliationResultDto> RepairAsync(Guid userId, Guid outletId, string reason, CancellationToken ct = default);
    Task<IReadOnlyList<StockReconciliationRunDto>> GetRunsAsync(Guid outletId, int take, CancellationToken ct = default);
    Task<StockReconciliationResultDto> GetRunAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StockReconciliationMismatchDto>> GetRunItemsAsync(Guid id, int page, int pageSize, CancellationToken ct = default);
}
