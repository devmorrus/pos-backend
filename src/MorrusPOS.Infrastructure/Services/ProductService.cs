using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Application.Features.Capabilities;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICapabilityEvaluator? _capabilities;

    public ProductService(
        AppDbContext dbContext,
        ICurrentUserService currentUserService,
        ICapabilityEvaluator? capabilities = null)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _capabilities = capabilities;
    }

    public async Task<ProductDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _dbContext.Products
            .Include(p => p.Brand).Include(p => p.Model)
            .Include(p => p.InventoryStocks)
            .Include(p => p.Variants)
                .ThenInclude(v => v.AttributeValues)
                    .ThenInclude(av => av.Attribute)
            .Include(p => p.Recipes)
                .ThenInclude(r => r.RawMaterialProduct)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (product == null)
        {
            throw new InvalidOperationException("Produk tidak ditemukan.");
        }

        Guid? outletId = _currentUserService.OutletId
            ?? product.InventoryStocks.Select(stock => (Guid?)stock.StockLocationId).FirstOrDefault();
        var stock = product.InventoryStocks.FirstOrDefault(s => s.StockLocationId == outletId && s.ProductVariantId == null);
        var qtyOnHand = product.HasVariants
            ? product.InventoryStocks.Where(s => s.StockLocationId == outletId && s.ProductVariantId != null).Sum(s => s.QtyOnHand)
            : stock?.QtyOnHand ?? 0;

        return MapToDto(product, qtyOnHand, outletId);
    }

    public async Task<IReadOnlyList<ProductDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default)
    {
        var products = await _dbContext.Products
            .Include(p => p.Brand).Include(p => p.Model)
            .Include(p => p.InventoryStocks)
            .Include(p => p.Variants)
                .ThenInclude(v => v.AttributeValues)
                    .ThenInclude(av => av.Attribute)
            .Include(p => p.Recipes)
                .ThenInclude(r => r.RawMaterialProduct)
            .Where(p => p.IsActive)
            .AsNoTracking()
            .ToListAsync(ct);

        return products.Select(p => {
            var stock = p.InventoryStocks.FirstOrDefault(s => s.StockLocationId == outletId && s.ProductVariantId == null);
            var qtyOnHand = p.HasVariants
                ? p.InventoryStocks.Where(s => s.StockLocationId == outletId && s.ProductVariantId != null).Sum(s => s.QtyOnHand)
                : stock?.QtyOnHand ?? 0;
            return MapToDto(p, qtyOnHand, outletId);
        }).ToList();
    }

    public async Task<IReadOnlyList<ProductDto>> GetRawMaterialsAsync(CancellationToken ct = default)
    {
        var products = await _dbContext.Products
            .Include(p => p.Brand).Include(p => p.Model)
            .Include(p => p.Variants)
                .ThenInclude(v => v.AttributeValues)
                    .ThenInclude(av => av.Attribute)
            .Where(p => p.IsActive && p.IsRawMaterial)
            .AsNoTracking()
            .ToListAsync(ct);

        return products.Select(p => MapToDto(p, 0)).ToList();
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        request = request with { Sku = request.Sku.Trim().ToUpperInvariant(), Barcode = NormalizeBarcode(request.Barcode) };
        await ValidateMastersAsync(request.BrandId, request.ModelId, null, ct);
        await ValidateIdentifiersAsync(null, request.Sku, request.Barcode, request.HasVariants ? request.Variants : null, ct);
        await EnsureOptionalCapabilitiesAsync(request.HasVariants, request.Variants, request.Recipes, request.IsConsignment, ct);
        var trackingMode = NormalizeTrackingMode(request.TrackingMode);
        await ValidateTrackingModeAsync(trackingMode, request.IsConsignment, request.IsRawMaterial, ct);
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
            BrandId = request.BrandId,
            ModelId = request.ModelId,
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
            TrackingMode = trackingMode,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Products.Add(newProduct);

        // 4. Auto-seed inventory stock (0 qty) for all outlets
        var outlets = await _dbContext.Outlets.ToListAsync(ct);
        foreach (var outlet in outlets.Where(_ => trackingMode != ProductTrackingModes.None))
        {
            _dbContext.InventoryStocks.Add(new InventoryStock
            {
                Id = Guid.NewGuid(),
                StockLocationId = outlet.Id,
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
                    Sku = vReq.Sku.Trim().ToUpperInvariant(),
                    Barcode = NormalizeBarcode(vReq.Barcode),
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
                foreach (var outlet in outlets.Where(_ => trackingMode != ProductTrackingModes.None))
                {
                    _dbContext.InventoryStocks.Add(new InventoryStock
                    {
                        Id = Guid.NewGuid(),
                        StockLocationId = outlet.Id,
                        ProductId = newProduct.Id,
                        ProductVariantId = variant.Id,
                        QtyOnHand = 0,
                        MinStockAlert = 0,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        // 5.5 Handle recipes
        if (!request.IsRawMaterial && request.Recipes != null)
        {
            foreach (var rReq in request.Recipes)
            {
                Guid? variantId = null;
                if (!string.IsNullOrEmpty(rReq.ProductVariantSku))
                {
                    var matchingVariant = _dbContext.ProductVariants.Local
                        .FirstOrDefault(v => v.ProductId == newProduct.Id && v.Sku.ToLower() == rReq.ProductVariantSku.ToLower());
                    if (matchingVariant != null)
                    {
                        variantId = matchingVariant.Id;
                    }
                }

                _dbContext.ProductRecipes.Add(new ProductRecipe
                {
                    Id = Guid.NewGuid(),
                    ProductId = newProduct.Id,
                    ProductVariantId = variantId,
                    RawMaterialProductId = rReq.RawMaterialProductId,
                    QuantityRequired = rReq.QuantityRequired
                });
            }
        }

        // 6. Create Audit Log entry
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUserService.UserId ?? Guid.Empty,
            OutletId = _currentUserService.OutletId ?? outlets.FirstOrDefault()?.Id
                ?? throw new InvalidOperationException("Business belum memiliki outlet untuk mencatat audit produk."),
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
        request = request with { Sku = request.Sku.Trim().ToUpperInvariant(), Barcode = NormalizeBarcode(request.Barcode) };
        await EnsureOptionalCapabilitiesAsync(request.HasVariants, request.Variants, request.Recipes, request.IsConsignment, ct);
        var product = await _dbContext.Products
            .Include(p => p.InventoryStocks)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (product == null)
        {
            throw new InvalidOperationException("Produk tidak ditemukan.");
        }

        var trackingMode = request.TrackingMode is null
            ? product.TrackingMode
            : NormalizeTrackingMode(request.TrackingMode);
        await ValidateMastersAsync(request.BrandId, request.ModelId, product, ct);
        await ValidateIdentifiersAsync(id, request.Sku, request.Barcode, request.HasVariants ? request.Variants : null, ct);
        if (product.Sku != request.Sku || product.Barcode != request.Barcode)
            await EnsureIdentityCanChangeAsync(id, ct);
        if (product.Sku != request.Sku || product.Barcode != request.Barcode || product.BrandId != request.BrandId || product.ModelId != request.ModelId)
        {
            _dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = _currentUserService.UserId ?? Guid.Empty,
                OutletId = await ResolveAuditOutletIdAsync(ct), EntityType = "product", EntityId = id,
                Action = "catalog_identity_change",
                OldValueJson = JsonSerializer.Serialize(new { product.Sku, product.Barcode, product.BrandId, product.ModelId }),
                NewValueJson = JsonSerializer.Serialize(new { request.Sku, request.Barcode, request.BrandId, request.ModelId })
            });
        }
        await ValidateTrackingModeAsync(trackingMode, request.IsConsignment, request.IsRawMaterial, ct);
        var trackingModeChanged = trackingMode != product.TrackingMode;
        if (trackingModeChanged)
            await EnsureTrackingModeCanChangeAsync(product, trackingMode, ct);

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
            var auditOutletId = await ResolveAuditOutletIdAsync(ct);
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = _currentUserService.UserId ?? Guid.Empty,
                OutletId = auditOutletId,
                EntityType = "product",
                EntityId = product.Id,
                Action = "price_change",
                OldValueJson = JsonSerializer.Serialize(oldValues),
                NewValueJson = JsonSerializer.Serialize(changes),
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.AuditLogs.Add(auditLog);
        }

        if (trackingModeChanged)
        {
            var auditOutletId = await ResolveAuditOutletIdAsync(ct);
            _dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = _currentUserService.UserId ?? Guid.Empty,
                OutletId = auditOutletId,
                EntityType = "product",
                EntityId = product.Id,
                Action = "tracking_mode_change",
                OldValueJson = JsonSerializer.Serialize(new { TrackingMode = product.TrackingMode }),
                NewValueJson = JsonSerializer.Serialize(new { TrackingMode = trackingMode }),
                CreatedAt = DateTime.UtcNow
            });
        }

        product.CategoryId = request.CategoryId;
        product.BrandId = request.BrandId;
        product.ModelId = request.ModelId;
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
        product.TrackingMode = trackingMode;
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
                    var hasStockHistory =
                        await _dbContext.StockLedgers.AnyAsync(x => x.ProductVariantId == existing.Id, ct) ||
                        await _dbContext.ProductBatches.AnyAsync(x => x.ProductVariantId == existing.Id, ct) ||
                        await _dbContext.ProductSerials.AnyAsync(x => x.ProductVariantId == existing.Id, ct) ||
                        await _dbContext.StockTransferItems.AnyAsync(x => x.ProductVariantId == existing.Id, ct) ||
                        await _dbContext.StockOpnameItems.AnyAsync(x => x.ProductVariantId == existing.Id, ct);
                    if (hasSales || hasStockHistory)
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
                    if (existing.Barcode != NormalizeBarcode(reqVariant.Barcode)) await EnsureIdentityCanChangeAsync(id, ct);
                    existing.Barcode = NormalizeBarcode(reqVariant.Barcode);
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
                        Sku = vReq.Sku.Trim().ToUpperInvariant(),
                        Barcode = NormalizeBarcode(vReq.Barcode),
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
                    foreach (var outlet in outletsList.Where(_ => product.TrackingMode != ProductTrackingModes.None))
                    {
                        _dbContext.InventoryStocks.Add(new InventoryStock
                        {
                            Id = Guid.NewGuid(),
                            StockLocationId = outlet.Id,
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

        // 5.5 Sync recipes
        if (!request.IsRawMaterial)
        {
            var existingRecipes = await _dbContext.ProductRecipes
                .Where(r => r.ProductId == id)
                .ToListAsync(ct);
            _dbContext.ProductRecipes.RemoveRange(existingRecipes);

            if (request.Recipes != null)
            {
                foreach (var rReq in request.Recipes)
                {
                    Guid? variantId = null;
                    if (!string.IsNullOrEmpty(rReq.ProductVariantSku))
                    {
                        var matchingVariant = _dbContext.ProductVariants.Local
                            .FirstOrDefault(v => v.ProductId == id && v.Sku.ToLower() == rReq.ProductVariantSku.ToLower())
                            ?? await _dbContext.ProductVariants
                                .FirstOrDefaultAsync(v => v.ProductId == id && v.Sku.ToLower() == rReq.ProductVariantSku.ToLower(), ct);
                        
                        if (matchingVariant != null)
                        {
                            variantId = matchingVariant.Id;
                        }
                    }

                    _dbContext.ProductRecipes.Add(new ProductRecipe
                    {
                        Id = Guid.NewGuid(),
                        ProductId = id,
                        ProductVariantId = variantId,
                        RawMaterialProductId = rReq.RawMaterialProductId,
                        QuantityRequired = rReq.QuantityRequired
                    });
                }
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(product.Id, ct);
    }

    private async Task EnsureOptionalCapabilitiesAsync(
        bool hasVariants,
        IReadOnlyList<CreateProductVariantRequest>? variants,
        IReadOnlyList<ProductRecipeRequest>? recipes,
        bool isConsignment,
        CancellationToken ct)
    {
        if (_capabilities is null) return;
        if (hasVariants || variants?.Count > 0)
            await _capabilities.EnsureEnabledAsync(CapabilityCodes.CatalogVariants, null, ct);
        if (recipes?.Count > 0)
            await _capabilities.EnsureEnabledAsync(CapabilityCodes.CatalogRecipes, null, ct);
        if (isConsignment)
            await _capabilities.EnsureEnabledAsync(CapabilityCodes.Consignment, null, ct);
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

        // Safe delete: keep history when the product has sales or any stock/tracking trail.
        var hasSales = await _dbContext.TransactionItems.AnyAsync(ti => ti.ProductId == id, ct);
        var hasStockHistory =
            product.InventoryStocks.Any(s => s.QtyOnHand != 0) ||
            await _dbContext.StockLedgers.AnyAsync(x => x.ProductId == id, ct) ||
            await _dbContext.ProductBatches.AnyAsync(x => x.ProductId == id, ct) ||
            await _dbContext.ProductSerials.AnyAsync(x => x.ProductId == id, ct) ||
            await _dbContext.StockTransferItems.AnyAsync(x => x.ProductId == id, ct) ||
            await _dbContext.StockOpnameItems.AnyAsync(x => x.ProductId == id, ct);
        if (hasSales || hasStockHistory)
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

    private async Task<Guid> ResolveAuditOutletIdAsync(CancellationToken ct)
    {
        if (_currentUserService.OutletId.HasValue)
            return _currentUserService.OutletId.Value;

        var outletId = await _dbContext.Outlets
            .Where(outlet => outlet.IsActive)
            .OrderBy(outlet => outlet.CreatedAt)
            .Select(outlet => (Guid?)outlet.Id)
            .FirstOrDefaultAsync(ct);
        return outletId ?? throw new InvalidOperationException(
            "Business belum memiliki outlet untuk mencatat audit produk.");
    }

    private static ProductDto MapToDto(Product p, decimal qtyOnHand, Guid? outletId = null)
    {
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
            p.Variants.Select(v => new ProductVariantDto(
                v.Id, v.ProductId, v.Sku, v.Barcode, v.BasePrice, v.CostPrice,
                v.ImageUrl, v.IsActive,
                v.AttributeValues.Select(x => new ProductAttributeValueDto(x.Attribute?.Name ?? string.Empty, x.Value)).ToList()
            )
            {
                QtyOnHand = outletId.HasValue
                    ? p.InventoryStocks.FirstOrDefault(s => s.StockLocationId == outletId && s.ProductVariantId == v.Id)?.QtyOnHand ?? 0
                    : 0
            }).ToList(),
            p.TrackingMode,
            p.BrandId, p.Brand?.Name, p.ModelId, p.Model?.Name
        )
        {
            Recipes = new List<ProductRecipeDto>()
        };
    }

    public async Task<ProductTrackingOptionsDto> GetTrackingOptionsAsync(
        Guid productId, Guid outletId, Guid? productVariantId, string? search, int take, bool includeUnavailable = false,
        CancellationToken ct = default)
    {
        var product = await _dbContext.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == productId, ct)
            ?? throw new KeyNotFoundException("Produk tidak ditemukan.");
        var stock = await _dbContext.InventoryStocks.AsNoTracking()
            .Where(x => x.ProductId == productId && x.ProductVariantId == productVariantId && x.StockLocationId == outletId)
            .Select(x => (decimal?)x.QtyOnHand).FirstOrDefaultAsync(ct) ?? 0;
        var safeTake = Math.Clamp(take, 1, 100);
        var term = search?.Trim();

        var batches = product.TrackingMode == ProductTrackingModes.Batch
            ? await _dbContext.ProductBatches.AsNoTracking()
                .Where(x => x.ProductId == productId && x.ProductVariantId == productVariantId &&
                            x.StockLocationId == outletId && (includeUnavailable || (x.QtyRemaining > 0 &&
                            (!x.ExpiryDate.HasValue || x.ExpiryDate.Value.Date >= DateTime.UtcNow.Date))) &&
                            (string.IsNullOrEmpty(term) || x.BatchNumber.Contains(term)))
                .OrderBy(x => x.ExpiryDate == null).ThenBy(x => x.ExpiryDate).ThenBy(x => x.CreatedAt)
                .Take(safeTake)
                .Select(x => new ProductBatchOptionDto(x.Id, x.BatchNumber, x.ExpiryDate, x.QtyRemaining))
                .ToListAsync(ct)
            : [];
        var serials = product.TrackingMode == ProductTrackingModes.Serial
            ? await _dbContext.ProductSerials.AsNoTracking()
                .Where(x => x.ProductId == productId && x.ProductVariantId == productVariantId &&
                            x.StockLocationId == outletId && x.Status == ProductSerialStatuses.Available &&
                            (string.IsNullOrEmpty(term) || x.NormalizedSerialNumber.Contains(term!.ToUpper())))
                .OrderBy(x => x.SerialNumber).Take(safeTake)
                .Select(x => new ProductSerialOptionDto(x.Id, x.SerialNumber)).ToListAsync(ct)
            : [];
        return new ProductTrackingOptionsDto(product.TrackingMode, stock, batches, serials);
    }

    private static string? NormalizeBarcode(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task ValidateMastersAsync(Guid? brandId, Guid? modelId, Product? existing, CancellationToken ct)
    {
        if (modelId.HasValue && !brandId.HasValue)
            throw new CatalogValidationException("PRODUCT_MODEL_BRAND_MISMATCH", "Pilih brand sebelum memilih model.");
        if (brandId.HasValue)
        {
            var brand = await _dbContext.ProductBrands.SingleOrDefaultAsync(x => x.Id == brandId, ct);
            if (brand is null || (!brand.IsActive && existing?.BrandId != brandId))
                throw new CatalogValidationException("PRODUCT_BRAND_INVALID", "Brand tidak valid atau nonaktif.");
        }
        if (modelId.HasValue)
        {
            var model = await _dbContext.ProductModels.SingleOrDefaultAsync(x => x.Id == modelId, ct);
            if (model is null || (!model.IsActive && existing?.ModelId != modelId))
                throw new CatalogValidationException("PRODUCT_MODEL_INVALID", "Model tidak valid atau nonaktif.");
            if (model.BrandId != brandId)
                throw new CatalogValidationException("PRODUCT_MODEL_BRAND_MISMATCH", "Model bukan milik brand yang dipilih.");
        }
    }

    private async Task EnsureIdentityCanChangeAsync(Guid id, CancellationToken ct)
    {
        if (await _dbContext.TransactionItems.AnyAsync(x => x.ProductId == id, ct) ||
            await _dbContext.StockLedgers.AnyAsync(x => x.ProductId == id, ct) ||
            await _dbContext.InventoryStocks.AnyAsync(x => x.ProductId == id && x.QtyOnHand != 0, ct) ||
            await _dbContext.PurchaseOrderItems.AnyAsync(x => x.ProductId == id, ct) ||
            await _dbContext.ConsignmentItems.AnyAsync(x => x.ProductId == id, ct) ||
            await _dbContext.GoBizProductMappings.AnyAsync(x => x.ProductId == id, ct) ||
            await _dbContext.ProductBatches.AnyAsync(x => x.ProductId == id, ct) ||
            await _dbContext.ProductSerials.AnyAsync(x => x.ProductId == id, ct))
            throw new CatalogValidationException("PRODUCT_IDENTITY_LOCKED", "SKU/barcode dikunci karena produk memiliki stok, riwayat, atau mapping channel. Buat produk pengganti jika diperlukan.");
    }

    private async Task ValidateIdentifiersAsync(Guid? id, string sku, string? barcode, IReadOnlyList<CreateProductVariantRequest>? variants, CancellationToken ct)
    {
        var skus = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sku };
        var barcodes = new HashSet<string>(StringComparer.Ordinal);
        if (barcode is not null) barcodes.Add(barcode);
        foreach (var variant in variants ?? [])
        {
            var value = variant.Sku?.Trim() ?? "";
            if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[a-zA-Z0-9_-]{3,50}$"))
                throw new InvalidOperationException("SKU varian harus 3–50 karakter huruf, angka, strip, atau underscore.");
            if (!skus.Add(value)) throw new CatalogValidationException("PRODUCT_SKU_DUPLICATE", "SKU produk dan varian harus berbeda.");
            var code = NormalizeBarcode(variant.Barcode);
            if (code is not null && !System.Text.RegularExpressions.Regex.IsMatch(code, @"^[0-9]{8,18}$"))
                throw new InvalidOperationException("Barcode varian harus 8–18 digit.");
            if (code is not null && !barcodes.Add(code)) throw new CatalogValidationException("PRODUCT_BARCODE_DUPLICATE", "Barcode produk dan varian harus berbeda.");
        }
        var normalizedSkus = skus.Select(x => x.ToUpperInvariant()).ToArray();
        var codes = barcodes.ToArray();
        if (await _dbContext.Products.AnyAsync(x => x.Id != id && normalizedSkus.Contains(x.Sku.Trim().ToUpper()), ct) ||
            await _dbContext.ProductVariants.AnyAsync(x => x.ProductId != id && normalizedSkus.Contains(x.Sku.Trim().ToUpper()), ct))
            throw new CatalogValidationException("PRODUCT_SKU_DUPLICATE", "SKU sudah digunakan produk atau varian lain.");
        if (await _dbContext.Products.AnyAsync(x => x.Id != id && x.Barcode != null && codes.Contains(x.Barcode.Trim()), ct) ||
            await _dbContext.ProductVariants.AnyAsync(x => x.ProductId != id && x.Barcode != null && codes.Contains(x.Barcode.Trim()), ct))
            throw new CatalogValidationException("PRODUCT_BARCODE_DUPLICATE", "Barcode sudah digunakan produk atau varian lain.");
        // Retained inactive variants still reserve their identifiers.
        var retained = await _dbContext.ProductVariants.Where(x => x.ProductId == id).AsNoTracking().ToListAsync(ct);
        if (retained.Any(x => x.Sku.Trim().Equals(sku, StringComparison.OrdinalIgnoreCase)))
            throw new CatalogValidationException("PRODUCT_SKU_DUPLICATE", "SKU sudah digunakan varian produk ini.");
        if (barcode is not null && retained.Any(x => NormalizeBarcode(x.Barcode) == barcode))
            throw new CatalogValidationException("PRODUCT_BARCODE_DUPLICATE", "Barcode sudah digunakan varian produk ini.");
    }

    private static string NormalizeTrackingMode(string? mode)
    {
        var normalized = string.IsNullOrWhiteSpace(mode)
            ? ProductTrackingModes.Quantity
            : mode.Trim().ToLowerInvariant();
        if (!ProductTrackingModes.All.Contains(normalized))
            throw new TrackingValidationException(TrackingErrorCodes.TrackingModeInvalid, "Tracking mode tidak valid.");
        return normalized;
    }

    private async Task ValidateTrackingModeAsync(string mode, bool isConsignment, bool isRawMaterial, CancellationToken ct)
    {
        if (mode == ProductTrackingModes.None && (isConsignment || isRawMaterial))
            throw new TrackingValidationException(TrackingErrorCodes.TrackingModeInvalid,
                "Produk tanpa tracking tidak dapat menjadi konsinyasi atau bahan baku.");
        if (_capabilities is null) return;
        if (mode == ProductTrackingModes.Batch)
            await _capabilities.EnsureEnabledAsync(CapabilityCodes.CatalogBatchExpiry, null, ct);
        if (mode == ProductTrackingModes.Serial)
            await _capabilities.EnsureEnabledAsync(CapabilityCodes.CatalogSerialTracking, null, ct);
    }

    private async Task EnsureTrackingModeCanChangeAsync(Product product, string nextMode, CancellationToken ct)
    {
        if (product.InventoryStocks.Any(x => x.QtyOnHand != 0) ||
            await _dbContext.ProductBatches.AnyAsync(x => x.ProductId == product.Id && x.QtyRemaining != 0, ct) ||
            await _dbContext.ProductSerials.AnyAsync(x => x.ProductId == product.Id &&
                (x.Status == ProductSerialStatuses.Available || x.Status == ProductSerialStatuses.InTransfer), ct))
            throw new InvalidOperationException("Tracking mode tidak dapat diubah karena masih ada stok aktif.");

        if (await _dbContext.StockTransfers.AnyAsync(t =>
                t.Status == StockTransferStatus.Pending && t.Items.Any(i => i.ProductId == product.Id), ct))
            throw new InvalidOperationException("Tracking mode tidak dapat diubah karena masih ada transfer stok yang menunggu.");

        if (await _dbContext.StockOpnames.AnyAsync(o =>
                o.Status == "draft" && o.Items.Any(i => i.ProductId == product.Id), ct))
            throw new InvalidOperationException("Tracking mode tidak dapat diubah karena masih ada stok opname berstatus draft.");

        if (await _dbContext.PurchaseOrders.AnyAsync(po =>
                (po.Status == PurchaseOrderStatus.Draft || po.Status == PurchaseOrderStatus.Pending ||
                 po.Status == PurchaseOrderStatus.PartiallyReceived) &&
                po.Items.Any(i => i.ProductId == product.Id), ct))
            throw new InvalidOperationException("Tracking mode tidak dapat diubah karena masih ada purchase order aktif.");

        if (nextMode == ProductTrackingModes.None)
        {
            // none products must not keep a stock balance row.
            _dbContext.InventoryStocks.RemoveRange(product.InventoryStocks);
            return;
        }

        if (product.TrackingMode == ProductTrackingModes.None)
        {
            var outlets = await _dbContext.Outlets.AsNoTracking().ToListAsync(ct);
            var variantIds = await _dbContext.ProductVariants
                .Where(v => v.ProductId == product.Id && v.IsActive)
                .Select(v => (Guid?)v.Id)
                .ToListAsync(ct);
            variantIds.Insert(0, null);

            foreach (var variantId in variantIds)
            foreach (var outlet in outlets)
                _dbContext.InventoryStocks.Add(new InventoryStock
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    ProductVariantId = variantId,
                    StockLocationId = outlet.Id,
                    QtyOnHand = 0,
                    MinStockAlert = 0,
                    UpdatedAt = DateTime.UtcNow
                });
        }
    }
}
