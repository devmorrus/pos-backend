namespace MorrusPOS.Application.Features.Products;

public record ProductDto(
    Guid Id,
    Guid CategoryId,
    string Sku,
    string Name,
    string? Barcode,
    decimal BasePrice,
    string Unit,
    bool IsConsignment,
    decimal QtyOnHand // hasil join ke InventoryStock untuk outlet aktif
);

public record CreateProductRequest(
    Guid CategoryId,
    string Sku,
    string Name,
    string? Barcode,
    decimal BasePrice,
    decimal CostPrice,
    string Unit,
    bool IsConsignment
);

public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
}
