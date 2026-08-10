using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Stock;
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
            .Where(stock => stock.OutletId == outletId && stock.Product.IsActive);

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
                (stock.Product.Barcode != null && stock.Product.Barcode.ToLower().Contains(normalizedSearch)));
        }

        var rows = await query
            .OrderBy(stock => stock.Product.Name)
            .Select(stock => new InventoryListItemDto(
                stock.ProductId,
                stock.Product.Sku,
                stock.Product.Name,
                stock.Product.CategoryId,
                stock.Product.Category.Name,
                stock.Product.Barcode,
                stock.Product.Unit,
                stock.Product.IsConsignment,
                stock.QtyOnHand,
                stock.MinStockAlert,
                stock.QtyOnHand <= stock.MinStockAlert,
                stock.Product.CostPrice,
                stock.Product.BasePrice,
                stock.UpdatedAt
            ))
            .ToListAsync(ct);

        return rows;
    }
}
