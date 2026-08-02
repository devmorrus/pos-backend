using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _dbContext;

    public CategoryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CategoryDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _dbContext.Categories
            .Include(c => c.Parent)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (category == null)
        {
            throw new InvalidOperationException("Kategori tidak ditemukan.");
        }

        return MapToDto(category);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await _dbContext.Categories
            .Include(c => c.Parent)
            .AsNoTracking()
            .ToListAsync(ct);

        return categories.Select(MapToDto).ToList();
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        // 1. Name uniqueness check
        var nameExists = await _dbContext.Categories
            .AnyAsync(c => c.Name.ToLower() == request.Name.ToLower(), ct);
        if (nameExists)
        {
            throw new InvalidOperationException("Nama kategori sudah digunakan.");
        }

        // 2. Validate Parent
        if (request.ParentId.HasValue)
        {
            var parentExists = await _dbContext.Categories.AnyAsync(c => c.Id == request.ParentId.Value, ct);
            if (!parentExists)
            {
                throw new InvalidOperationException("Kategori induk tidak valid.");
            }
        }

        var newCategory = new Category
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ParentId = request.ParentId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Categories.Add(newCategory);
        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(newCategory.Id, ct);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category == null)
        {
            throw new InvalidOperationException("Kategori tidak ditemukan.");
        }

        // 1. Check circular parent reference
        if (request.ParentId.HasValue && request.ParentId.Value == id)
        {
            throw new InvalidOperationException("Kategori tidak boleh menjadi induk dari dirinya sendiri.");
        }

        // 2. Name uniqueness excluding current category
        if (category.Name.ToLower() != request.Name.ToLower())
        {
            var nameExists = await _dbContext.Categories
                .AnyAsync(c => c.Name.ToLower() == request.Name.ToLower() && c.Id != id, ct);
            if (nameExists)
            {
                throw new InvalidOperationException("Nama kategori sudah digunakan pada kategori lain.");
            }
        }

        // 3. Validate Parent
        if (request.ParentId.HasValue)
        {
            var parentExists = await _dbContext.Categories.AnyAsync(c => c.Id == request.ParentId.Value, ct);
            if (!parentExists)
            {
                throw new InvalidOperationException("Kategori induk tidak valid.");
            }
        }

        category.Name = request.Name;
        category.ParentId = request.ParentId;
        category.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(category.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _dbContext.Categories
            .Include(c => c.Children)
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (category == null)
        {
            throw new InvalidOperationException("Kategori tidak ditemukan.");
        }

        // Prevent delete if category has children sub-categories
        if (category.Children.Any())
        {
            throw new InvalidOperationException("Kategori tidak bisa dihapus karena masih memiliki sub-kategori di dalamnya.");
        }

        // Prevent delete if category has products
        if (category.Products.Any(p => p.IsActive))
        {
            throw new InvalidOperationException("Kategori tidak bisa dihapus karena masih digunakan oleh produk aktif.");
        }

        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync(ct);
    }

    private static CategoryDto MapToDto(Category category)
    {
        return new CategoryDto(
            category.Id,
            category.Name,
            category.ParentId,
            category.Parent?.Name,
            category.CreatedAt,
            category.UpdatedAt
        );
    }
}
