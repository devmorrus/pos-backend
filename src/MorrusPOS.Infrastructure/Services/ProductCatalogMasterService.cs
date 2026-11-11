using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class ProductCatalogMasterService(AppDbContext db) : IProductCatalogMasterService
{
    public async Task<IReadOnlyList<ProductBrandDto>> GetBrandsAsync(bool includeInactive, string? search, CancellationToken ct)
    {
        var term = search?.Trim().ToUpperInvariant();
        return await db.ProductBrands.AsNoTracking()
            .Where(x => (includeInactive || x.IsActive) && (string.IsNullOrEmpty(term) || x.NormalizedName.Contains(term)))
            .OrderBy(x => x.Name)
            .Select(x => new ProductBrandDto(x.Id, x.Name, x.IsActive,
                db.ProductModels.Count(m => m.BrandId == x.Id), db.Products.Count(p => p.BrandId == x.Id), x.CreatedAt, x.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<ProductBrandDto> GetBrandAsync(Guid id, CancellationToken ct)
        => (await GetBrandsAsync(true, null, ct)).FirstOrDefault(x => x.Id == id)
            ?? throw new KeyNotFoundException("Brand tidak ditemukan.");

    public async Task<ProductBrandDto> SaveBrandAsync(Guid? id, SaveProductBrandRequest request, CancellationToken ct)
    {
        var name = ValidateName(request.Name, 100);
        var normalized = name.ToUpperInvariant();
        if (await db.ProductBrands.AnyAsync(x => x.Id != id && x.NormalizedName == normalized, ct))
            throw new CatalogValidationException("PRODUCT_BRAND_DUPLICATE", "Nama brand sudah digunakan.");
        var brand = id.HasValue
            ? await db.ProductBrands.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Brand tidak ditemukan.")
            : new ProductBrand();
        if (!id.HasValue) db.ProductBrands.Add(brand);
        brand.Name = name;
        brand.NormalizedName = normalized;
        brand.IsActive = request.IsActive;
        brand.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetBrandAsync(brand.Id, ct);
    }

    public async Task DeleteBrandAsync(Guid id, CancellationToken ct)
    {
        var brand = await db.ProductBrands.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Brand tidak ditemukan.");
        if (await db.ProductModels.AnyAsync(x => x.BrandId == id, ct) || await db.Products.AnyAsync(x => x.BrandId == id, ct))
            throw new CatalogValidationException("PRODUCT_MASTER_IN_USE", "Brand masih memiliki model atau digunakan produk. Nonaktifkan brand jika tidak lagi digunakan.");
        db.ProductBrands.Remove(brand);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ProductModelDto>> GetModelsAsync(Guid? brandId, bool includeInactive, string? search, CancellationToken ct)
    {
        var term = search?.Trim().ToUpperInvariant();
        return await db.ProductModels.AsNoTracking()
            .Where(x => (!brandId.HasValue || x.BrandId == brandId) && (includeInactive || (x.IsActive && x.Brand.IsActive)) &&
                (string.IsNullOrEmpty(term) || x.NormalizedName.Contains(term)))
            .OrderBy(x => x.Brand.Name).ThenBy(x => x.Name)
            .Select(x => new ProductModelDto(x.Id, x.BrandId, x.Brand.Name, x.Name, x.IsActive,
                db.Products.Count(p => p.ModelId == x.Id), x.CreatedAt, x.UpdatedAt)).ToListAsync(ct);
    }

    public async Task<ProductModelDto> GetModelAsync(Guid id, CancellationToken ct)
        => (await GetModelsAsync(null, true, null, ct)).FirstOrDefault(x => x.Id == id)
            ?? throw new KeyNotFoundException("Model tidak ditemukan.");

    public async Task<ProductModelDto> SaveModelAsync(Guid? id, SaveProductModelRequest request, CancellationToken ct)
    {
        var name = ValidateName(request.Name, 120);
        var brand = await db.ProductBrands.SingleOrDefaultAsync(x => x.Id == request.BrandId, ct)
            ?? throw new CatalogValidationException("PRODUCT_BRAND_INVALID", "Brand tidak valid.");
        var model = id.HasValue
            ? await db.ProductModels.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Model tidak ditemukan.")
            : new ProductModel();
        if (!brand.IsActive && (!id.HasValue || model.BrandId != brand.Id || (!model.IsActive && request.IsActive)))
            throw new CatalogValidationException("PRODUCT_BRAND_INVALID", "Brand nonaktif tidak dapat digunakan untuk model baru/diaktifkan kembali.");
        if (id.HasValue && model.BrandId != brand.Id && await db.Products.AnyAsync(x => x.ModelId == id, ct))
            throw new CatalogValidationException("PRODUCT_MASTER_IN_USE", "Model yang digunakan produk tidak dapat dipindahkan ke brand lain.");
        var normalized = name.ToUpperInvariant();
        if (await db.ProductModels.AnyAsync(x => x.Id != id && x.BrandId == brand.Id && x.NormalizedName == normalized, ct))
            throw new CatalogValidationException("PRODUCT_MODEL_DUPLICATE", "Nama model sudah digunakan pada brand ini.");
        if (!id.HasValue) db.ProductModels.Add(model);
        model.BrandId = brand.Id;
        model.Name = name;
        model.NormalizedName = normalized;
        model.IsActive = request.IsActive;
        model.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetModelAsync(model.Id, ct);
    }

    public async Task DeleteModelAsync(Guid id, CancellationToken ct)
    {
        var model = await db.ProductModels.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Model tidak ditemukan.");
        if (await db.Products.AnyAsync(x => x.ModelId == id, ct))
            throw new CatalogValidationException("PRODUCT_MASTER_IN_USE", "Model masih digunakan produk. Nonaktifkan model jika tidak lagi digunakan.");
        db.ProductModels.Remove(model);
        await db.SaveChangesAsync(ct);
    }

    private static string ValidateName(string? name, int max)
    {
        var value = name?.Trim() ?? "";
        if (value.Length == 0 || value.Length > max)
            throw new InvalidOperationException($"Nama wajib diisi, maksimal {max} karakter.");
        return value;
    }

    public async Task<string> GenerateBarcodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var digits = "20" + string.Concat(Enumerable.Range(0, 10).Select(_ => RandomNumberGenerator.GetInt32(10)));
            var sum = digits.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
            var barcode = digits + ((10 - sum % 10) % 10);
            if (!await db.ProductIdentifiers.AnyAsync(x => x.IdentifierType == "barcode" && x.NormalizedValue == barcode, ct) &&
                !await db.Products.AnyAsync(x => x.Barcode == barcode, ct) &&
                !await db.ProductVariants.AnyAsync(x => x.Barcode == barcode, ct)) return barcode;
        }
        throw new InvalidOperationException("Gagal membuat barcode unik. Silakan coba lagi.");
    }
}
