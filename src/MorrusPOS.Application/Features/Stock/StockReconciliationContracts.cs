namespace MorrusPOS.Application.Features.Stock;

public record PreviewStockReconciliationRequest(Guid StockLocationId);
public record RepairStockReconciliationRequest(Guid StockLocationId, string Reason);

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
    Guid StockLocationId,
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
    Guid StockLocationId,
    string StockLocationName,
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
    Task<StockReconciliationResultDto> PreviewAsync(Guid userId, Guid stockLocationId, CancellationToken ct = default);
    Task<StockReconciliationResultDto> RepairAsync(Guid userId, Guid stockLocationId, string reason, CancellationToken ct = default);
    Task<IReadOnlyList<StockReconciliationRunDto>> GetRunsAsync(Guid stockLocationId, int take, CancellationToken ct = default);
    Task<StockReconciliationResultDto> GetRunAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StockReconciliationMismatchDto>> GetRunItemsAsync(Guid id, int page, int pageSize, CancellationToken ct = default);
}
