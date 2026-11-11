using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public sealed class StockAvailabilityService : IStockAvailabilityService
{
    private readonly AppDbContext _db;

    public StockAvailabilityService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<StockAvailabilityKey, decimal>> GetReservedQuantitiesAsync(
        Guid stockLocationId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.StockReservationItems.AsNoTracking()
            .Where(x => x.StockLocationId == stockLocationId &&
                        x.Status == StockReservationStatus.Active &&
                        x.StockReservation.Status == StockReservationStatus.Active &&
                        x.StockReservation.ExpiresAt > now)
            .GroupBy(x => new { x.ProductId, x.ProductVariantId })
            .Select(g => new { g.Key.ProductId, g.Key.ProductVariantId, Qty = g.Sum(x => x.Qty) })
            .ToListAsync(ct);
        return rows.ToDictionary(x => new StockAvailabilityKey(x.ProductId, x.ProductVariantId), x => x.Qty);
    }

    public async Task<IReadOnlyDictionary<StockAvailabilityKey, decimal>> GetSoldQuantitiesAsync(
        Guid stockLocationId, CancellationToken ct = default)
    {
        var rows = await _db.StockLedgers.AsNoTracking()
            .Where(x => x.StockLocationId == stockLocationId &&
                        (x.MovementType == StockMovementType.Sale || x.MovementType == StockMovementType.Return))
            .GroupBy(x => new { x.ProductId, x.ProductVariantId })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductVariantId,
                Qty = -g.Sum(x => x.QtyChange)
            })
            .ToListAsync(ct);
        return rows.ToDictionary(x => new StockAvailabilityKey(x.ProductId, x.ProductVariantId), x => Math.Max(0, x.Qty));
    }

    public Task<decimal> GetReservedQuantityAsync(
        Guid stockLocationId, Guid productId, Guid? productVariantId, Guid? excludingReservationId = null,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return _db.StockReservationItems.AsNoTracking()
            .Where(x => x.StockLocationId == stockLocationId && x.ProductId == productId &&
                        x.ProductVariantId == productVariantId &&
                        x.Status == StockReservationStatus.Active &&
                        x.StockReservation.Status == StockReservationStatus.Active &&
                        x.StockReservation.ExpiresAt > now &&
                        (!excludingReservationId.HasValue || x.StockReservationId != excludingReservationId.Value))
            .SumAsync(x => x.Qty, ct);
    }

    public Task<decimal> GetBatchReservedQuantityAsync(
        Guid productBatchId, Guid? excludingReservationId = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return _db.StockReservationItems.AsNoTracking()
            .Where(x => x.ProductBatchId == productBatchId &&
                        x.Status == StockReservationStatus.Active &&
                        x.StockReservation.Status == StockReservationStatus.Active &&
                        x.StockReservation.ExpiresAt > now &&
                        (!excludingReservationId.HasValue || x.StockReservationId != excludingReservationId.Value))
            .SumAsync(x => x.Qty, ct);
    }
}
