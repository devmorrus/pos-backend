namespace MorrusPOS.Application.Features.Stock;

public record StockOpnameItemDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal SystemQty,
    decimal PhysicalQty,
    decimal Variance
);

public record StockOpnameDto(
    Guid Id,
    Guid OutletId,
    string OutletName,
    Guid PerformedBy,
    string PerformedByName,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<StockOpnameItemDto> Items
);

public record StockOpnameItemRequest(
    Guid ProductId,
    decimal PhysicalQty
);

public record CreateStockOpnameRequest(
    Guid OutletId,
    List<StockOpnameItemRequest> Items
);

public interface IStockOpnameService
{
    Task<StockOpnameDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StockOpnameDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default);
    Task<StockOpnameDto> CreateAsync(Guid userId, CreateStockOpnameRequest request, CancellationToken ct = default);
}
