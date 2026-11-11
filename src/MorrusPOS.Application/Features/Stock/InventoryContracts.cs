namespace MorrusPOS.Application.Features.Stock;

public record InventoryListItemDto(
    Guid ProductId,
    Guid? ProductVariantId,
    string Sku,
    string ProductName,
    Guid CategoryId,
    string CategoryName,
    string? Barcode,
    string Unit,
    bool IsConsignment,
    decimal QtyOnHand,
    decimal QtyReserved,
    decimal QtyAvailable,
    decimal QtySold,
    decimal MinStockAlert,
    bool IsLowStock,
    decimal CostPrice,
    decimal BasePrice,
    DateTime UpdatedAt
)
{
    public string TrackingMode { get; set; } = "quantity";
    public int TrackedItemCount { get; set; }
}

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryListItemDto>> GetByStockLocationAsync(
        Guid stockLocationId,
        string? search = null,
        bool lowStockOnly = false,
        bool includeZeroStock = true,
        CancellationToken ct = default);
}
