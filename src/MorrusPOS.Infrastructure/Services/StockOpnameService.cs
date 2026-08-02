using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class StockOpnameService : IStockOpnameService
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockService;
    private readonly IPosNotificationService _notificationService;

    public StockOpnameService(
        AppDbContext dbContext,
        IStockService stockService,
        IPosNotificationService notificationService)
    {
        _dbContext = dbContext;
        _stockService = stockService;
        _notificationService = notificationService;
    }

    public async Task<StockOpnameDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var opname = await _dbContext.StockOpnames
            .Include(o => o.Outlet)
            .Include(o => o.PerformedByUser)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (opname == null)
        {
            throw new InvalidOperationException("Laporan Stok Opname tidak ditemukan.");
        }

        return MapToDto(opname);
    }

    public async Task<IReadOnlyList<StockOpnameDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default)
    {
        var opnames = await _dbContext.StockOpnames
            .Include(o => o.Outlet)
            .Include(o => o.PerformedByUser)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => o.OutletId == outletId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return opnames.Select(MapToDto).ToList();
    }

    public async Task<StockOpnameDto> CreateAsync(Guid userId, CreateStockOpnameRequest request, CancellationToken ct = default)
    {
        var opname = new StockOpname
        {
            Id = Guid.NewGuid(),
            OutletId = request.OutletId,
            PerformedBy = userId,
            Status = "completed",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.StockOpnames.Add(opname);

        var stockUpdates = new List<StockUpdateItem>();

        foreach (var itemReq in request.Items)
        {
            var product = await _dbContext.Products.FindAsync(new object[] { itemReq.ProductId }, ct);
            if (product == null || !product.IsActive)
            {
                throw new InvalidOperationException($"Produk tidak valid.");
            }

            var stock = await _dbContext.InventoryStocks
                .FirstOrDefaultAsync(s => s.ProductId == itemReq.ProductId && s.OutletId == request.OutletId, ct);

            decimal systemQty = stock?.QtyOnHand ?? 0;
            decimal variance = itemReq.PhysicalQty - systemQty;

            var opnameItem = new StockOpnameItem
            {
                Id = Guid.NewGuid(),
                StockOpnameId = opname.Id,
                ProductId = itemReq.ProductId,
                SystemQty = systemQty,
                PhysicalQty = itemReq.PhysicalQty,
                Variance = variance
            };

            _dbContext.StockOpnameItems.Add(opnameItem);

            // Record movement and update stock only if there is a variance
            if (variance != 0)
            {
                await _stockService.AddMovementAsync(
                    productId: itemReq.ProductId,
                    outletId: request.OutletId,
                    qtyChange: variance,
                    movementType: StockMovementType.OpnameAdjustment,
                    referenceType: "stock_opname",
                    referenceId: opname.Id,
                    note: $"Penyesuaian stok opname fisik {itemReq.PhysicalQty} vs sistem {systemQty}",
                    ct: ct
                );

                stockUpdates.Add(new StockUpdateItem(itemReq.ProductId, variance));
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        // Broadcast changes via SignalR
        if (stockUpdates.Any())
        {
            await _notificationService.SendStockUpdateAsync(request.OutletId, stockUpdates, ct);
        }

        return await GetByIdAsync(opname.Id, ct);
    }

    private static StockOpnameDto MapToDto(StockOpname o)
    {
        return new StockOpnameDto(
            o.Id,
            o.OutletId,
            o.Outlet?.Name ?? string.Empty,
            o.PerformedBy,
            o.PerformedByUser?.Name ?? string.Empty,
            o.Status,
            o.CreatedAt,
            o.Items.Select(i => new StockOpnameItemDto(
                i.ProductId,
                i.Product?.Name ?? string.Empty,
                i.Product?.Sku ?? string.Empty,
                i.SystemQty,
                i.PhysicalQty,
                i.Variance
            )).ToList()
        );
    }
}
