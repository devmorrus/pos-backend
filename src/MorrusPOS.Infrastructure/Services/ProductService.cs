using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private static readonly Guid DefaultOutletId = Guid.Parse("8bba5427-017e-40fb-886f-5e4c6c9a3809"); // Outlet Utama fallback

    public ProductService(AppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<ProductDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _dbContext.Products
            .Include(p => p.InventoryStocks)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (product == null)
        {
            throw new InvalidOperationException("Produk tidak ditemukan.");
        }

        var outletId = _currentUserService.OutletId ?? DefaultOutletId;
        var stock = product.InventoryStocks.FirstOrDefault(s => s.OutletId == outletId);

        return MapToDto(product, stock?.QtyOnHand ?? 0);
    }

    public async Task<IReadOnlyList<ProductDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default)
    {
        var products = await _dbContext.Products
            .Include(p => p.InventoryStocks)
            .Where(p => p.IsActive)
            .AsNoTracking()
            .ToListAsync(ct);

        return products.Select(p => {
            var stock = p.InventoryStocks.FirstOrDefault(s => s.OutletId == outletId);
            return MapToDto(p, stock?.QtyOnHand ?? 0);
        }).ToList();
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        // 1. SKU uniqueness check
        var skuExists = await _dbContext.Products
            .AnyAsync(p => p.Sku.ToLower() == request.Sku.ToLower(), ct);
        if (skuExists)
        {
            throw new InvalidOperationException("SKU sudah terdaftar.");
        }

        // 2. Barcode uniqueness check if provided
        if (!string.IsNullOrEmpty(request.Barcode))
        {
            var barcodeExists = await _dbContext.Products
                .AnyAsync(p => p.Barcode != null && p.Barcode.ToLower() == request.Barcode.ToLower(), ct);
            if (barcodeExists)
            {
                throw new InvalidOperationException("Barcode sudah terdaftar.");
            }
        }

        // 3. Category Validation
        var categoryExists = await _dbContext.Categories.AnyAsync(c => c.Id == request.CategoryId, ct);
        if (!categoryExists)
        {
            throw new InvalidOperationException("Kategori tidak valid.");
        }

        var newProduct = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            Sku = request.Sku,
            Name = request.Name,
            Barcode = request.Barcode,
            BasePrice = request.BasePrice,
            CostPrice = request.CostPrice,
            Unit = request.Unit,
            IsConsignment = request.IsConsignment,
            ImageUrl = request.ImageUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Products.Add(newProduct);

        // 4. Auto-seed inventory stock (0 qty) for all outlets
        var outlets = await _dbContext.Outlets.ToListAsync(ct);
        foreach (var outlet in outlets)
        {
            _dbContext.InventoryStocks.Add(new InventoryStock
            {
                Id = Guid.NewGuid(),
                OutletId = outlet.Id,
                ProductId = newProduct.Id,
                QtyOnHand = 0,
                MinStockAlert = 0,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // 5. Create Audit Log entry
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUserService.UserId ?? Guid.Empty,
            OutletId = _currentUserService.OutletId ?? DefaultOutletId,
            EntityType = "product",
            EntityId = newProduct.Id,
            Action = "create",
            OldValueJson = null,
            NewValueJson = JsonSerializer.Serialize(new
            {
                newProduct.Sku,
                newProduct.Name,
                newProduct.BasePrice,
                newProduct.CostPrice,
                newProduct.IsConsignment
            }),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AuditLogs.Add(auditLog);

        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(newProduct.Id, ct);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _dbContext.Products
            .Include(p => p.InventoryStocks)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (product == null)
        {
            throw new InvalidOperationException("Produk tidak ditemukan.");
        }

        // 1. SKU uniqueness excluding current product
        if (product.Sku.ToLower() != request.Sku.ToLower())
        {
            var skuExists = await _dbContext.Products
                .AnyAsync(p => p.Sku.ToLower() == request.Sku.ToLower() && p.Id != id, ct);
            if (skuExists)
            {
                throw new InvalidOperationException("SKU sudah terdaftar pada produk lain.");
            }
        }

        // 2. Barcode uniqueness excluding current product
        if (!string.IsNullOrEmpty(request.Barcode) && product.Barcode?.ToLower() != request.Barcode.ToLower())
        {
            var barcodeExists = await _dbContext.Products
                .AnyAsync(p => p.Barcode != null && p.Barcode.ToLower() == request.Barcode.ToLower() && p.Id != id, ct);
            if (barcodeExists)
            {
                throw new InvalidOperationException("Barcode sudah terdaftar pada produk lain.");
            }
        }

        // 3. Category Validation
        var categoryExists = await _dbContext.Categories.AnyAsync(c => c.Id == request.CategoryId, ct);
        if (!categoryExists)
        {
            throw new InvalidOperationException("Kategori tidak valid.");
        }

        // 4. Audit Price Changes (BasePrice & CostPrice)
        var changes = new Dictionary<string, object>();
        var oldValues = new Dictionary<string, object>();

        if (product.BasePrice != request.BasePrice)
        {
            oldValues["BasePrice"] = product.BasePrice;
            changes["BasePrice"] = request.BasePrice;
        }

        if (product.CostPrice != request.CostPrice)
        {
            oldValues["CostPrice"] = product.CostPrice;
            changes["CostPrice"] = request.CostPrice;
        }

        if (changes.Any())
        {
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = _currentUserService.UserId ?? Guid.Empty,
                OutletId = _currentUserService.OutletId ?? DefaultOutletId,
                EntityType = "product",
                EntityId = product.Id,
                Action = "price_change",
                OldValueJson = JsonSerializer.Serialize(oldValues),
                NewValueJson = JsonSerializer.Serialize(changes),
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.AuditLogs.Add(auditLog);
        }

        product.CategoryId = request.CategoryId;
        product.Sku = request.Sku;
        product.Name = request.Name;
        product.Barcode = request.Barcode;
        product.BasePrice = request.BasePrice;
        product.CostPrice = request.CostPrice;
        product.Unit = request.Unit;
        product.IsConsignment = request.IsConsignment;
        product.ImageUrl = request.ImageUrl;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var outletId = _currentUserService.OutletId ?? DefaultOutletId;
        var stock = product.InventoryStocks.FirstOrDefault(s => s.OutletId == outletId);

        return MapToDto(product, stock?.QtyOnHand ?? 0);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _dbContext.Products
            .Include(p => p.InventoryStocks)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (product == null)
        {
            throw new InvalidOperationException("Produk tidak ditemukan.");
        }

        // Safe delete: check relation to transaction_items
        var hasSales = await _dbContext.TransactionItems.AnyAsync(ti => ti.ProductId == id, ct);
        if (hasSales)
        {
            // Soft delete/deactivate to preserve historical sales reporting
            product.IsActive = false;
            product.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Hard delete product & associated inventory stock rows
            _dbContext.InventoryStocks.RemoveRange(product.InventoryStocks);
            _dbContext.Products.Remove(product);
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    private static ProductDto MapToDto(Product p, decimal qtyOnHand)
    {
        return new ProductDto(
            p.Id,
            p.CategoryId,
            p.Sku,
            p.Name,
            p.Barcode,
            p.BasePrice,
            p.Unit,
            p.IsConsignment,
            qtyOnHand,
            p.ImageUrl
        );
    }
}
