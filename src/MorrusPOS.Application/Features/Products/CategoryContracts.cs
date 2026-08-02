namespace MorrusPOS.Application.Features.Products;

public record CategoryDto(
    Guid Id,
    string Name,
    Guid? ParentId,
    string? ParentName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateCategoryRequest(
    string Name,
    Guid? ParentId
);

public record UpdateCategoryRequest(
    string Name,
    Guid? ParentId
);

public interface ICategoryService
{
    Task<CategoryDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
