using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class BufferStockService : IBufferStockService
{
    private readonly AppDbContext _dbContext;
    private readonly IOnlineStockAvailabilityService _availabilityService;

    public BufferStockService(
        AppDbContext dbContext,
        IOnlineStockAvailabilityService availabilityService)
    {
        _dbContext = dbContext;
        _availabilityService = availabilityService;
    }

    public async Task<IReadOnlyList<BufferStockListItemDto>> GetByOutletAsync(
        Guid outletId,
        string? search = null,
        CancellationToken ct = default)
    {
        var outlet = await _dbContext.Outlets
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == outletId, ct)
            ?? throw new InvalidOperationException("Outlet tidak valid.");

        var normalizedSearch = search?.Trim().ToLowerInvariant();

        var stocksQuery = _dbContext.InventoryStocks
            .AsNoTracking()
            .Include(s => s.Product)
                .ThenInclude(p => p.Category)
            .Include(s => s.ProductVariant)
                .ThenInclude(v => v!.AttributeValues)
            .Where(s => s.OutletId == outletId && s.Product.IsActive);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            stocksQuery = stocksQuery.Where(s =>
                s.Product.Name.ToLower().Contains(normalizedSearch) ||
                s.Product.Sku.ToLower().Contains(normalizedSearch) ||
                (s.Product.Barcode != null && s.Product.Barcode.ToLower().Contains(normalizedSearch)) ||
                (s.ProductVariant != null && s.ProductVariant.Sku.ToLower().Contains(normalizedSearch)));
        }

        var stocks = await stocksQuery
            .OrderBy(s => s.Product.Name)
            .ThenBy(s => s.ProductVariant != null ? s.ProductVariant.Sku : string.Empty)
            .ToListAsync(ct);

        var policies = await _dbContext.ChannelStockPolicies
            .AsNoTracking()
            .Where(p => p.OutletId == outletId)
            .ToListAsync(ct);

        var variantPolicies = policies
            .Where(p => p.ProductVariantId.HasValue)
            .ToDictionary(
                p => (p.ProductId, p.ProductVariantId!.Value),
                p => p);

        var productPolicies = policies
            .Where(p => !p.ProductVariantId.HasValue)
            .ToDictionary(p => p.ProductId, p => p);

        var result = new List<BufferStockListItemDto>(stocks.Count);

        foreach (var stock in stocks)
        {
            ChannelStockPolicy? effectivePolicy = null;

            if (stock.ProductVariantId.HasValue &&
                variantPolicies.TryGetValue((stock.ProductId, stock.ProductVariantId.Value), out var vp))
            {
                effectivePolicy = vp;
            }
            else if (productPolicies.TryGetValue(stock.ProductId, out var pp))
            {
                // Fallback produk induk untuk varian maupun parent.
                effectivePolicy = pp;
            }

            var bufferQty = effectivePolicy?.IsEnabled == true || effectivePolicy == null && true
                ? (effectivePolicy?.BufferQty ?? 0)
                : 0;

            // Jika policy disabled, buffer dianggap 0.
            if (effectivePolicy != null && !effectivePolicy.IsEnabled)
            {
                bufferQty = 0;
            }

            var isEnabled = effectivePolicy?.IsEnabled ?? true;
            var available = _availabilityService.CalculateAvailableOnlineQty(
                stock.QtyOnHand,
                effectivePolicy?.BufferQty ?? 0,
                isEnabled);

            var isVariantRow = stock.ProductVariantId.HasValue;
            var sku = isVariantRow && stock.ProductVariant != null
                ? stock.ProductVariant.Sku
                : stock.Product.Sku;

            string? variantName = null;
            if (isVariantRow && stock.ProductVariant != null)
            {
                if (stock.ProductVariant.AttributeValues.Count > 0)
                {
                    variantName = string.Join(" / ",
                        stock.ProductVariant.AttributeValues.Select(v => v.Value));
                }
                else
                {
                    variantName = stock.ProductVariant.Sku;
                }
            }

            result.Add(new BufferStockListItemDto(
                stock.ProductId,
                stock.ProductVariantId,
                sku,
                stock.Product.Name,
                variantName,
                stock.Product.CategoryId,
                stock.Product.Category.Name,
                stock.Product.Unit,
                stock.QtyOnHand,
                bufferQty,
                available,
                available > 0,
                isEnabled,
                stock.UpdatedAt,
                effectivePolicy?.UpdatedAt));
        }

        return result;
    }

    public async Task<OnlineStockAvailabilityDto> GetAvailabilityAsync(
        Guid outletId,
        Guid productId,
        Guid? productVariantId = null,
        CancellationToken ct = default)
    {
        var stock = await _dbContext.InventoryStocks
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.OutletId == outletId &&
                     s.ProductId == productId &&
                     s.ProductVariantId == productVariantId,
                ct);

        var qtyOnHand = stock?.QtyOnHand ?? 0;

        var policy = await _dbContext.ChannelStockPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.OutletId == outletId &&
                     p.ProductId == productId &&
                     p.ProductVariantId == productVariantId,
                ct);

        // Fallback ke policy produk induk untuk varian.
        if (policy == null && productVariantId.HasValue)
        {
            policy = await _dbContext.ChannelStockPolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.OutletId == outletId &&
                         p.ProductId == productId &&
                         p.ProductVariantId == null,
                    ct);
        }

        var bufferQty = policy?.BufferQty ?? 0;
        var isEnabled = policy?.IsEnabled ?? true;
        var available = _availabilityService.CalculateAvailableOnlineQty(qtyOnHand, bufferQty, isEnabled);

        return new OnlineStockAvailabilityDto(
            outletId,
            productId,
            productVariantId,
            qtyOnHand,
            isEnabled ? bufferQty : 0,
            available,
            available > 0);
    }

    public async Task<BufferStockListItemDto> UpsertAsync(
        Guid userId,
        UpsertBufferStockRequest request,
        CancellationToken ct = default)
    {
        if (request.BufferQty < 0)
        {
            throw new InvalidOperationException("Buffer stock tidak boleh negatif.");
        }

        var outlet = await _dbContext.Outlets
            .FirstOrDefaultAsync(o => o.Id == request.OutletId, ct)
            ?? throw new InvalidOperationException("Outlet tidak valid.");

        var product = await _dbContext.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, ct)
            ?? throw new InvalidOperationException("Produk tidak ditemukan.");

        if (!product.IsActive)
        {
            throw new InvalidOperationException("Produk tidak aktif.");
        }

        // Pastikan produk dan outlet berada pada business yang sama (jika keduanya punya BusinessId).
        if (outlet.BusinessId.HasValue && product.BusinessId.HasValue &&
            outlet.BusinessId.Value != product.BusinessId.Value)
        {
            throw new UnauthorizedAccessException("Produk tidak berada pada business outlet tersebut.");
        }

        ProductVariant? variant = null;
        if (request.ProductVariantId.HasValue)
        {
            variant = await _dbContext.ProductVariants
                .FirstOrDefaultAsync(v => v.Id == request.ProductVariantId.Value, ct)
                ?? throw new InvalidOperationException("Varian produk tidak ditemukan.");

            if (variant.ProductId != request.ProductId)
            {
                throw new InvalidOperationException("Varian tidak dimiliki produk tersebut.");
            }
        }

        var existing = await _dbContext.ChannelStockPolicies
            .FirstOrDefaultAsync(
                p => p.OutletId == request.OutletId &&
                     p.ProductId == request.ProductId &&
                     p.ProductVariantId == request.ProductVariantId,
                ct);

        var now = DateTime.UtcNow;

        if (existing is null)
        {
            existing = new ChannelStockPolicy
            {
                Id = Guid.NewGuid(),
                OutletId = request.OutletId,
                ProductId = request.ProductId,
                ProductVariantId = request.ProductVariantId,
                BufferQty = request.BufferQty,
                IsEnabled = request.IsEnabled,
                UpdatedBy = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.ChannelStockPolicies.Add(existing);
        }
        else
        {
            existing.BufferQty = request.BufferQty;
            existing.IsEnabled = request.IsEnabled;
            existing.UpdatedBy = userId;
            existing.UpdatedAt = now;
        }

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Race condition: dua request membuat policy yang sama.
            // Ambil ulang dan update.
            var raced = await _dbContext.ChannelStockPolicies
                .FirstOrDefaultAsync(
                    p => p.OutletId == request.OutletId &&
                         p.ProductId == request.ProductId &&
                         p.ProductVariantId == request.ProductVariantId,
                    ct)
                ?? throw new InvalidOperationException("Gagal menyimpan buffer stock karena duplikasi data.");

            raced.BufferQty = request.BufferQty;
            raced.IsEnabled = request.IsEnabled;
            raced.UpdatedBy = userId;
            raced.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            existing = raced;
        }

        // TIDAK menyentuh InventoryStock.QtyOnHand dan TIDAK menulis StockLedger.
        // Ambil ulang availability terbaru.
        var availability = await GetAvailabilityAsync(
            request.OutletId,
            request.ProductId,
            request.ProductVariantId,
            ct);

        var variantName = variant != null ? variant.Sku : (string?)null;

        return new BufferStockListItemDto(
            request.ProductId,
            request.ProductVariantId,
            variant?.Sku ?? product.Sku,
            product.Name,
            variantName,
            product.CategoryId,
            product.Category.Name,
            product.Unit,
            availability.QtyOnHand,
            availability.BufferQty,
            availability.AvailableOnlineQty,
            availability.IsOnlineAvailable,
            request.IsEnabled,
            now,
            existing.UpdatedAt);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("23505");
    }
}
