namespace MorrusPOS.Application.Features.Stock;

public record StockOpnameItemDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal SystemQty,
    decimal PhysicalQty,
    decimal Variance
)
{
    public Guid? ProductVariantId { get; init; }
    public Guid? ProductBatchId { get; init; }
    public string? BatchNumber { get; init; }
    public Guid? ProductSerialId { get; init; }
    public string? SerialNumber { get; init; }
}

public record StockOpnameDto(
    Guid Id,
    Guid StockLocationId,
    string StockLocationName,
    string StockLocationType,
    Guid PerformedBy,
    string PerformedByName,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<StockOpnameItemDto> Items
);

public record StockOpnameItemRequest(
    Guid ProductId,
    decimal PhysicalQty
)
{
    public Guid? ProductVariantId { get; init; }
    public Guid? ProductBatchId { get; init; }
    // For serial tracked products: the serials physically present at opname time.
    public IReadOnlyList<Guid>? ProductSerialIds { get; init; }
}

public record CreateStockOpnameRequest(
    Guid StockLocationId,
    List<StockOpnameItemRequest> Items
);

public interface IStockOpnameService
{
    Task<StockOpnameDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StockOpnameDto>> GetByStockLocationAsync(Guid stockLocationId, CancellationToken ct = default);
    Task<StockOpnameDto> CreateAsync(Guid userId, CreateStockOpnameRequest request, CancellationToken ct = default);
}
