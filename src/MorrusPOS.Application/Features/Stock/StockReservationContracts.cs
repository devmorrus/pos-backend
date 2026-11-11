namespace MorrusPOS.Application.Features.Stock;

public readonly record struct StockAvailabilityKey(Guid ProductId, Guid? ProductVariantId);

public record StockAvailabilityDto(
    decimal QtyOnHand,
    decimal QtyReserved,
    decimal QtyAvailable,
    decimal QtySold);

public record StockReservationBatchRequest(Guid ProductBatchId, decimal Qty);

public record StockReservationItemRequest(
    Guid ProductId,
    decimal Qty)
{
    public Guid? ProductVariantId { get; init; }
    public IReadOnlyList<StockReservationBatchRequest>? BatchAllocations { get; init; }
    public IReadOnlyList<Guid>? SerialIds { get; init; }
}

public record CreateStockReservationRequest(
    Guid Id,
    Guid OutletId,
    Guid CashierSessionId,
    IReadOnlyList<StockReservationItemRequest> Items);

public record StockReservationItemDto(
    Guid Id,
    Guid ProductId,
    Guid? ProductVariantId,
    string ProductName,
    string Sku,
    decimal Qty,
    string TrackingMode,
    Guid? ProductBatchId,
    string? BatchNumber,
    Guid? ProductSerialId,
    string? SerialNumber,
    string Status);

public record StockReservationDto(
    Guid Id,
    Guid StockLocationId,
    string StockLocationName,
    Guid? CashierSessionId,
    Guid CreatedBy,
    string CreatedByName,
    string SourceType,
    string Status,
    DateTime ExpiresAt,
    DateTime? ResolvedAt,
    string? ReleaseReason,
    DateTime CreatedAt,
    IReadOnlyList<StockReservationItemDto> Items);

public interface IStockAvailabilityService
{
    Task<IReadOnlyDictionary<StockAvailabilityKey, decimal>> GetReservedQuantitiesAsync(
        Guid stockLocationId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<StockAvailabilityKey, decimal>> GetSoldQuantitiesAsync(
        Guid stockLocationId, CancellationToken ct = default);
    Task<decimal> GetReservedQuantityAsync(
        Guid stockLocationId, Guid productId, Guid? productVariantId, Guid? excludingReservationId = null,
        CancellationToken ct = default);
    Task<decimal> GetBatchReservedQuantityAsync(Guid productBatchId, Guid? excludingReservationId = null,
        CancellationToken ct = default);
}

public interface IStockReservationService
{
    Task<StockReservationDto> CreateAsync(CreateStockReservationRequest request, CancellationToken ct = default);
    Task<StockReservationDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StockReservationDto>> GetActiveAsync(Guid stockLocationId, Guid? productId = null,
        Guid? productVariantId = null, CancellationToken ct = default);
    Task<StockReservationDto> ReleaseAsync(Guid id, string? reason = null, CancellationToken ct = default);
}
