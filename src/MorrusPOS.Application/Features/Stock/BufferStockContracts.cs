namespace MorrusPOS.Application.Features.Stock;

public record BufferStockListItemDto(
    Guid ProductId,
    Guid? ProductVariantId,
    string Sku,
    string ProductName,
    string? VariantName,
    Guid CategoryId,
    string CategoryName,
    string Unit,
    decimal QtyOnHand,
    decimal BufferQty,
    decimal AvailableOnlineQty,
    bool IsOnlineAvailable,
    bool IsEnabled,
    DateTime UpdatedAt,
    DateTime? BufferUpdatedAt
);

public record UpsertBufferStockRequest(
    Guid OutletId,
    Guid ProductId,
    Guid? ProductVariantId,
    decimal BufferQty,
    bool IsEnabled
);

public record OnlineStockAvailabilityDto(
    Guid OutletId,
    Guid ProductId,
    Guid? ProductVariantId,
    decimal QtyOnHand,
    decimal BufferQty,
    decimal AvailableOnlineQty,
    bool IsOnlineAvailable
);

public readonly record struct OnlineStockKey(
    Guid ProductId,
    Guid? ProductVariantId);

public interface IBufferStockService
{
    Task<IReadOnlyList<BufferStockListItemDto>> GetByOutletAsync(
        Guid outletId,
        string? search = null,
        CancellationToken ct = default);

    Task<BufferStockListItemDto> UpsertAsync(
        Guid userId,
        UpsertBufferStockRequest request,
        CancellationToken ct = default);

    Task<OnlineStockAvailabilityDto> GetAvailabilityAsync(
        Guid outletId,
        Guid productId,
        Guid? productVariantId = null,
        CancellationToken ct = default);
}

public interface IOnlineStockAvailabilityService
{
    decimal CalculateAvailableOnlineQty(decimal qtyOnHand, decimal bufferQty, bool isEnabled);

    Task<IReadOnlyDictionary<OnlineStockKey, decimal>> GetAvailableQuantitiesAsync(
        Guid outletId,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<OnlineStockKey, OnlineStockAvailabilityDto>> GetAvailabilityMapAsync(
        Guid outletId,
        CancellationToken ct = default);
}
