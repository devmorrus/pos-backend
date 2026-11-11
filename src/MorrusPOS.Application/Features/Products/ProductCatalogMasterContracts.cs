namespace MorrusPOS.Application.Features.Products;

public record ProductBrandDto(Guid Id, string Name, bool IsActive, int ModelCount, int ProductCount, DateTime CreatedAt, DateTime UpdatedAt);
public record ProductModelDto(Guid Id, Guid BrandId, string BrandName, string Name, bool IsActive, int ProductCount, DateTime CreatedAt, DateTime UpdatedAt);
public record SaveProductBrandRequest(string Name, bool IsActive = true);
public record SaveProductModelRequest(Guid BrandId, string Name, bool IsActive = true);

public interface IProductCatalogMasterService
{
    Task<IReadOnlyList<ProductBrandDto>> GetBrandsAsync(bool includeInactive, string? search, CancellationToken ct);
    Task<ProductBrandDto> GetBrandAsync(Guid id, CancellationToken ct);
    Task<ProductBrandDto> SaveBrandAsync(Guid? id, SaveProductBrandRequest request, CancellationToken ct);
    Task DeleteBrandAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ProductModelDto>> GetModelsAsync(Guid? brandId, bool includeInactive, string? search, CancellationToken ct);
    Task<ProductModelDto> GetModelAsync(Guid id, CancellationToken ct);
    Task<ProductModelDto> SaveModelAsync(Guid? id, SaveProductModelRequest request, CancellationToken ct);
    Task DeleteModelAsync(Guid id, CancellationToken ct);
    Task<string> GenerateBarcodeAsync(CancellationToken ct);
}
