using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Events;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Outbox;

public sealed class StockEventPublisher : IStockEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxWriter _outbox;

    public StockEventPublisher(AppDbContext db, ICurrentUserService currentUser, IOutboxWriter outbox)
    {
        _db = db;
        _currentUser = currentUser;
        _outbox = outbox;
    }

    public async Task EnqueueCurrentStockAsync(
        Guid stockLocationId,
        IReadOnlyCollection<StockEventKey> items,
        string deduplicationKey,
        CancellationToken ct = default)
    {
        var businessId = _currentUser.BusinessId
            ?? throw new InvalidOperationException("Business context wajib tersedia untuk outbox stock event.");
        var keys = items.Distinct().ToArray();
        if (keys.Length == 0)
            return;

        var location = await _db.StockLocations.AsNoTracking()
            .Where(x => x.Id == stockLocationId)
            .Select(x => new { x.Id, x.OutletId, x.Type })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Lokasi stock event tidak valid.");

        var productIds = keys.Select(x => x.ProductId).Distinct().ToArray();
        var stocks = await _db.InventoryStocks.AsNoTracking()
            .Where(x => x.StockLocationId == stockLocationId && productIds.Contains(x.ProductId))
            .Select(x => new { x.ProductId, x.ProductVariantId, x.QtyOnHand })
            .ToListAsync(ct);
        var stockMap = stocks.ToDictionary(x => new StockEventKey(x.ProductId, x.ProductVariantId), x => x.QtyOnHand);
        var payload = new StockUpdatedEventV2(
            location.Id,
            location.OutletId,
            location.Type,
            keys.Select(key => new StockLevelV2(
                key.ProductId,
                key.ProductVariantId,
                stockMap.GetValueOrDefault(key))).ToArray());
        var occurredAt = DateTime.UtcNow;

        await _outbox.EnqueueAsync(new OutboxWriteRequest(
            Guid.NewGuid(),
            businessId,
            location.OutletId,
            OutboxEventTypes.StockUpdated,
            2,
            JsonSerializer.Serialize(payload, JsonOptions),
            deduplicationKey,
            Activity.Current?.TraceId.ToString(),
            occurredAt), ct);
    }
}
