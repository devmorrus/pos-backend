namespace MorrusPOS.Application.Features.Stock;

public record InventoryListItemDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    Guid CategoryId,
    string CategoryName,
    string? Barcode,
    string Unit,
    bool IsConsignment,
    decimal QtyOnHand,
    decimal MinStockAlert,
    bool IsLowStock,
    DateTime UpdatedAt
);

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryListItemDto>> GetByOutletAsync(
        Guid outletId,
        string? search = null,
        bool lowStockOnly = false,
        bool includeZeroStock = true,
        CancellationToken ct = default);
}
