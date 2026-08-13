namespace MorrusPOS.Application.Features.Products;

public record ProductAttributeValueDto(
    string AttributeName,
    string Value
);

public record ProductVariantDto(
    Guid Id,
    Guid ProductId,
    string Sku,
    string? Barcode,
    decimal BasePrice,
    decimal CostPrice,
    string? ImageUrl,
    bool IsActive,
    IReadOnlyList<ProductAttributeValueDto> AttributeValues
);

public record ProductDto(
    Guid Id,
    Guid CategoryId,
    string Sku,
    string Name,
    string? Barcode,
    decimal BasePrice,
    decimal CostPrice,
    string Unit,
    bool IsConsignment,
    decimal QtyOnHand, // hasil join ke InventoryStock untuk outlet aktif
    string? ImageUrl = null,
    bool? IsTaxable = null,
    bool? IsServiceChargeable = null,
    bool HasVariants = false,
    bool IsRawMaterial = false,
    IReadOnlyList<ProductVariantDto>? Variants = null
);

public record CreateProductAttributeValueRequest(
    string AttributeName,
    string Value
);

public record CreateProductVariantRequest(
    string Sku,
    string? Barcode,
    decimal BasePrice,
    decimal CostPrice,
    string? ImageUrl,
    IReadOnlyList<CreateProductAttributeValueRequest> AttributeValues
);

public record CreateProductRequest(
    Guid CategoryId,
    string Sku,
    string Name,
    string? Barcode,
    decimal BasePrice,
    decimal CostPrice,
    string Unit,
    bool IsConsignment,
    string? ImageUrl = null,
    bool? IsTaxable = null,
    bool? IsServiceChargeable = null,
    bool HasVariants = false,
    bool IsRawMaterial = false,
    IReadOnlyList<CreateProductVariantRequest>? Variants = null
);

public record UpdateProductRequest(
    Guid CategoryId,
    string Sku,
    string Name,
    string? Barcode,
    decimal BasePrice,
    decimal CostPrice,
    string Unit,
    bool IsConsignment,
    bool IsActive,
    string? ImageUrl = null,
    bool? IsTaxable = null,
    bool? IsServiceChargeable = null,
    bool HasVariants = false,
    bool IsRawMaterial = false,
    IReadOnlyList<CreateProductVariantRequest>? Variants = null
);

public interface IProductService
{
    Task<ProductDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ProductDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
    Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
