using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _dbContext;

    public InventoryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<InventoryListItemDto>> GetByOutletAsync(
        Guid outletId,
        string? search = null,
        bool lowStockOnly = false,
        bool includeZeroStock = true,
        CancellationToken ct = default)
    {
        var normalizedSearch = search?.Trim().ToLowerInvariant();

        var query = _dbContext.InventoryStocks
            .AsNoTracking()
            .Include(stock => stock.Product)
                .ThenInclude(product => product.Category)
            .Include(stock => stock.ProductVariant)
            // Untracked products never have a stock balance; drop legacy rows defensively.
            .Where(stock => stock.OutletId == outletId && stock.Product.IsActive &&
                            stock.Product.TrackingMode != ProductTrackingModes.None);

        if (!includeZeroStock)
        {
            query = query.Where(stock => stock.QtyOnHand > 0);
        }

        if (lowStockOnly)
        {
            query = query.Where(stock => stock.QtyOnHand <= stock.MinStockAlert);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(stock =>
                stock.Product.Name.ToLower().Contains(normalizedSearch) ||
                stock.Product.Sku.ToLower().Contains(normalizedSearch) ||
                (stock.Product.Barcode != null && stock.Product.Barcode.ToLower().Contains(normalizedSearch)) ||
                (stock.ProductVariant != null && stock.ProductVariant.Sku.ToLower().Contains(normalizedSearch)));
        }

        var rows = await query
            .OrderBy(stock => stock.Product.Name)
            .ThenBy(stock => stock.ProductVariant != null ? stock.ProductVariant.Sku : string.Empty)
            .Select(stock => new InventoryListItemDto(
                stock.ProductId,
                stock.ProductVariantId,
                stock.ProductVariant != null ? stock.ProductVariant.Sku : stock.Product.Sku,
                stock.Product.Name,
                stock.Product.CategoryId,
                stock.Product.Category.Name,
                stock.ProductVariant != null ? stock.ProductVariant.Barcode ?? stock.Product.Barcode : stock.Product.Barcode,
                stock.Product.Unit,
                stock.Product.IsConsignment,
                stock.QtyOnHand,
                stock.MinStockAlert,
                stock.QtyOnHand <= stock.MinStockAlert,
                stock.Product.CostPrice,
                stock.Product.BasePrice,
                stock.UpdatedAt
            )
            {
                TrackingMode = stock.Product.TrackingMode
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            return rows;
        }

        var batchCounts = await _dbContext.ProductBatches
            .AsNoTracking()
            .Where(x => x.OutletId == outletId && x.QtyRemaining > 0)
            .GroupBy(x => new { x.ProductId, x.ProductVariantId })
            .Select(g => new { g.Key.ProductId, g.Key.ProductVariantId, Count = g.Count() })
            .ToListAsync(ct);
        var serialCounts = await _dbContext.ProductSerials
            .AsNoTracking()
            .Where(x => x.OutletId == outletId && x.Status == ProductSerialStatuses.Available)
            .GroupBy(x => new { x.ProductId, x.ProductVariantId })
            .Select(g => new { g.Key.ProductId, g.Key.ProductVariantId, Count = g.Count() })
            .ToListAsync(ct);

        var batchMap = batchCounts.ToDictionary(x => (x.ProductId, x.ProductVariantId), x => x.Count);
        var serialMap = serialCounts.ToDictionary(x => (x.ProductId, x.ProductVariantId), x => x.Count);

        return rows.Select(row =>
        {
            row.TrackedItemCount = row.TrackingMode == ProductTrackingModes.Batch
                ? batchMap.GetValueOrDefault((row.ProductId, row.ProductVariantId))
                : row.TrackingMode == ProductTrackingModes.Serial
                    ? serialMap.GetValueOrDefault((row.ProductId, row.ProductVariantId))
                    : 0;
            return row;
        }).ToList();
    }
}
