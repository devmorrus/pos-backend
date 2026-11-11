using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class OnlineStockAvailabilityService : IOnlineStockAvailabilityService
{
    private readonly AppDbContext _dbContext;
    private readonly IStockAvailabilityService _stockAvailability;

    public OnlineStockAvailabilityService(AppDbContext dbContext, IStockAvailabilityService? stockAvailability = null)
    {
        _dbContext = dbContext;
        _stockAvailability = stockAvailability ?? new StockAvailabilityService(dbContext);
    }

    public decimal CalculateAvailableOnlineQty(decimal qtyOnHand, decimal bufferQty, bool isEnabled, decimal qtyReserved = 0)
    {
        if (!isEnabled)
        {
            return Math.Max(0, qtyOnHand - qtyReserved);
        }

        var effectiveBuffer = bufferQty < 0 ? 0 : bufferQty;
        return Math.Max(0, qtyOnHand - qtyReserved - effectiveBuffer);
    }

    public async Task<IReadOnlyDictionary<OnlineStockKey, decimal>> GetAvailableQuantitiesAsync(
        Guid outletId,
        CancellationToken ct = default)
    {
        var map = await GetAvailabilityMapAsync(outletId, ct);
        return map.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.AvailableOnlineQty);
    }

    public async Task<IReadOnlyDictionary<OnlineStockKey, OnlineStockAvailabilityDto>> GetAvailabilityMapAsync(
        Guid outletId,
        CancellationToken ct = default)
    {
        var stocks = await _dbContext.InventoryStocks
            .AsNoTracking()
            .Where(s => s.StockLocationId == outletId && s.Product.TrackingMode != ProductTrackingModes.None)
            .Select(s => new
            {
                s.ProductId,
                s.ProductVariantId,
                s.QtyOnHand
            })
            .ToListAsync(ct);

        var policies = await _dbContext.ChannelStockPolicies
            .AsNoTracking()
            .Where(p => p.OutletId == outletId)
            .Select(p => new
            {
                p.ProductId,
                p.ProductVariantId,
                p.BufferQty,
                p.IsEnabled
            })
            .ToListAsync(ct);

        // Index policies: variant-specific first, then product-level fallback.
        var variantPolicies = policies
            .Where(p => p.ProductVariantId.HasValue)
            .ToDictionary(
                p => new OnlineStockKey(p.ProductId, p.ProductVariantId),
                p => p);

        var productPolicies = policies
            .Where(p => !p.ProductVariantId.HasValue)
            .ToDictionary(
                p => new OnlineStockKey(p.ProductId, null),
                p => p);

        var result = new Dictionary<OnlineStockKey, OnlineStockAvailabilityDto>();
        var reserved = await _stockAvailability.GetReservedQuantitiesAsync(outletId, ct);

        foreach (var stock in stocks)
        {
            var key = new OnlineStockKey(stock.ProductId, stock.ProductVariantId);

            decimal bufferQty = 0;
            var isEnabled = true;

            if (variantPolicies.TryGetValue(key, out var variantPolicy))
            {
                bufferQty = variantPolicy.BufferQty;
                isEnabled = variantPolicy.IsEnabled;
            }
            else if (stock.ProductVariantId.HasValue &&
                     productPolicies.TryGetValue(new OnlineStockKey(stock.ProductId, null), out var fallbackPolicy))
            {
                // Fallback: buffer produk induk berlaku untuk varian
                // jika varian belum punya policy sendiri.
                bufferQty = fallbackPolicy.BufferQty;
                isEnabled = fallbackPolicy.IsEnabled;
            }
            else if (productPolicies.TryGetValue(key, out var productPolicy))
            {
                bufferQty = productPolicy.BufferQty;
                isEnabled = productPolicy.IsEnabled;
            }

            var qtyReserved = reserved.GetValueOrDefault(new StockAvailabilityKey(stock.ProductId, stock.ProductVariantId));
            var qtyAvailable = Math.Max(0, stock.QtyOnHand - qtyReserved);
            var available = CalculateAvailableOnlineQty(stock.QtyOnHand, bufferQty, isEnabled, qtyReserved);

            result[key] = new OnlineStockAvailabilityDto(
                outletId,
                stock.ProductId,
                stock.ProductVariantId,
                stock.QtyOnHand,
                isEnabled ? bufferQty : 0,
                available,
                available > 0)
            {
                QtyReserved = qtyReserved,
                QtyAvailable = qtyAvailable
            };
        }

        return result;
    }
}
