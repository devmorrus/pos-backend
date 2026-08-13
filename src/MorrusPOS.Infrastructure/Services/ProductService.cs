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
            .Include(p => p.Variants)
                .ThenInclude(v => v.AttributeValues)
                    .ThenInclude(av => av.Attribute)
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
            .Include(p => p.Variants)
                .ThenInclude(v => v.AttributeValues)
                    .ThenInclude(av => av.Attribute)
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
            IsTaxable = request.IsTaxable,
            IsServiceChargeable = request.IsServiceChargeable,
            IsActive = true,
            HasVariants = request.HasVariants,
            IsRawMaterial = request.IsRawMaterial,
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

        // 5. Handle variants and attribute mapping
        if (request.HasVariants && request.Variants != null)
        {
            foreach (var vReq in request.Variants)
            {
                var variant = new ProductVariant
                {
                    Id = Guid.NewGuid(),
                    ProductId = newProduct.Id,
                    Sku = vReq.Sku,
                    Barcode = vReq.Barcode,
                    BasePrice = vReq.BasePrice,
                    CostPrice = vReq.CostPrice,
                    ImageUrl = vReq.ImageUrl,
                    IsActive = true
                };

                foreach (var avReq in vReq.AttributeValues)
                {
                    var attribute = await _dbContext.ProductAttributes
                        .Include(a => a.Values)
                        .FirstOrDefaultAsync(a => a.Name.ToLower() == avReq.AttributeName.ToLower(), ct);

                    if (attribute == null)
                    {
                        attribute = new ProductAttribute
                        {
                            Id = Guid.NewGuid(),
                            Name = avReq.AttributeName
                        };
                        _dbContext.ProductAttributes.Add(attribute);
                    }

                    var attrValue = attribute.Values
                        .FirstOrDefault(v => v.Value.ToLower() == avReq.Value.ToLower());

                    if (attrValue == null)
                    {
                        attrValue = new ProductAttributeValue
                        {
                            Id = Guid.NewGuid(),
                            AttributeId = attribute.Id,
                            Value = avReq.Value
                        };
                        _dbContext.ProductAttributeValues.Add(attrValue);
                    }

                    variant.AttributeValues.Add(attrValue);
                }

                _dbContext.ProductVariants.Add(variant);

                // Seed stock for this variant
                foreach (var outlet in outlets)
                {
                    _dbContext.InventoryStocks.Add(new InventoryStock
                    {
                        Id = Guid.NewGuid(),
                        OutletId = outlet.Id,
                        ProductId = newProduct.Id,
                        ProductVariantId = variant.Id,
                        QtyOnHand = 0,
                        MinStockAlert = 0,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        // 6. Create Audit Log entry
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
        product.IsTaxable = request.IsTaxable;
        product.IsServiceChargeable = request.IsServiceChargeable;
        product.IsActive = request.IsActive;
        product.HasVariants = request.HasVariants;
        product.IsRawMaterial = request.IsRawMaterial;
        product.UpdatedAt = DateTime.UtcNow;

        // Sync product variants
        if (request.HasVariants && request.Variants != null)
        {
            var existingVariants = await _dbContext.ProductVariants
                .Include(v => v.AttributeValues)
                .Where(v => v.ProductId == id)
                .ToListAsync(ct);

            // Mark inactive or remove deleted variants
            foreach (var existing in existingVariants)
            {
                var reqVariant = request.Variants.FirstOrDefault(v => v.Sku.ToLower() == existing.Sku.ToLower());
                if (reqVariant == null)
                {
                    var hasSales = await _dbContext.TransactionItems.AnyAsync(ti => ti.ProductVariantId == existing.Id, ct);
                    if (hasSales)
                    {
                        existing.IsActive = false;
                    }
                    else
                    {
                        var stocks = await _dbContext.InventoryStocks.Where(s => s.ProductVariantId == existing.Id).ToListAsync(ct);
                        _dbContext.InventoryStocks.RemoveRange(stocks);
                        _dbContext.ProductVariants.Remove(existing);
                    }
                }
                else
                {
                    existing.BasePrice = reqVariant.BasePrice;
                    existing.CostPrice = reqVariant.CostPrice;
                    existing.Barcode = reqVariant.Barcode;
                    existing.ImageUrl = reqVariant.ImageUrl;
                    existing.IsActive = true;
                }
            }

            // Create new variants
            foreach (var vReq in request.Variants)
            {
                var exists = existingVariants.Any(v => v.Sku.ToLower() == vReq.Sku.ToLower());
                if (!exists)
                {
                    var variant = new ProductVariant
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        Sku = vReq.Sku,
                        Barcode = vReq.Barcode,
                        BasePrice = vReq.BasePrice,
                        CostPrice = vReq.CostPrice,
                        ImageUrl = vReq.ImageUrl,
                        IsActive = true
                    };

                    foreach (var avReq in vReq.AttributeValues)
                    {
                        var attribute = await _dbContext.ProductAttributes
                            .Include(a => a.Values)
                            .FirstOrDefaultAsync(a => a.Name.ToLower() == avReq.AttributeName.ToLower(), ct);

                        if (attribute == null)
                        {
                            attribute = new ProductAttribute
                            {
                                Id = Guid.NewGuid(),
                                Name = avReq.AttributeName
                            };
                            _dbContext.ProductAttributes.Add(attribute);
                        }

                        var attrValue = attribute.Values
                            .FirstOrDefault(v => v.Value.ToLower() == avReq.Value.ToLower());

                        if (attrValue == null)
                        {
                            attrValue = new ProductAttributeValue
                            {
                                Id = Guid.NewGuid(),
                                AttributeId = attribute.Id,
                                Value = avReq.Value
                            };
                            _dbContext.ProductAttributeValues.Add(attrValue);
                        }

                        variant.AttributeValues.Add(attrValue);
                    }

                    _dbContext.ProductVariants.Add(variant);

                    // Seed variant inventory stocks
                    var outletsList = await _dbContext.Outlets.ToListAsync(ct);
                    foreach (var outlet in outletsList)
                    {
                        _dbContext.InventoryStocks.Add(new InventoryStock
                        {
                            Id = Guid.NewGuid(),
                            OutletId = outlet.Id,
                            ProductId = product.Id,
                            ProductVariantId = variant.Id,
                            QtyOnHand = 0,
                            MinStockAlert = 0,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        var outletId = _currentUserService.OutletId ?? DefaultOutletId;
        var stock = product.InventoryStocks.FirstOrDefault(s => s.OutletId == outletId);

        return await GetByIdAsync(product.Id, ct);
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
        var variantDtos = p.Variants?.Select(v => new ProductVariantDto(
            v.Id,
            v.ProductId,
            v.Sku,
            v.Barcode,
            v.BasePrice,
            v.CostPrice,
            v.ImageUrl,
            v.IsActive,
            v.AttributeValues.Select(av => new ProductAttributeValueDto(
                av.Attribute?.Name ?? "",
                av.Value
            )).ToList()
        )).ToList();

        return new ProductDto(
            p.Id,
            p.CategoryId,
            p.Sku,
            p.Name,
            p.Barcode,
            p.BasePrice,
            p.CostPrice,
            p.Unit,
            p.IsConsignment,
            qtyOnHand,
            p.ImageUrl,
            p.IsTaxable,
            p.IsServiceChargeable,
            p.HasVariants,
            p.IsRawMaterial,
            variantDtos
        );
    }
}
