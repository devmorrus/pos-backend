using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MorrusPOS.Application.Common.Events;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Outbox;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Api.StockReservations;

public sealed class StockReservationExpiryBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _outboxOptions;
    private readonly ILogger<StockReservationExpiryBackgroundService> _logger;

    public StockReservationExpiryBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> outboxOptions,
        ILogger<StockReservationExpiryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _outboxOptions = outboxOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Gagal memproses expiry reservasi stok.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    private async Task ExpireBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SystemDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT "Id" AS "Value"
            FROM stock_reservations
            WHERE "Status"='active' AND "ExpiresAt"<={DateTime.UtcNow}
            ORDER BY "ExpiresAt"
            FOR UPDATE SKIP LOCKED
            LIMIT 100
            """).ToListAsync(ct);
        if (ids.Count == 0)
        {
            await transaction.CommitAsync(ct);
            return;
        }

        var reservations = await db.StockReservations
            .Include(x => x.StockLocation)
            .Include(x => x.Items).ThenInclude(x => x.ProductSerial)
            .Where(x => ids.Contains(x.Id) && x.Status == StockReservationStatus.Active)
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var reservation in reservations)
        {
            reservation.Status = StockReservationStatus.Expired;
            reservation.ResolvedAt = now;
            reservation.ReleaseReason = "Reservasi kedaluwarsa.";
            reservation.UpdatedAt = now;
            foreach (var item in reservation.Items)
            {
                item.Status = StockReservationStatus.Expired;
                item.ResolvedAt = now;
                if (item.ProductSerial is { Status: ProductSerialStatuses.Reserved } serial &&
                    !await db.StockReservationItems.AsNoTracking().AnyAsync(x =>
                        x.ProductSerialId == serial.Id && x.StockReservationId != reservation.Id &&
                        x.Status == StockReservationStatus.Active &&
                        x.StockReservation.Status == StockReservationStatus.Active &&
                        x.StockReservation.ExpiresAt > now, ct))
                {
                    serial.Status = ProductSerialStatuses.Available;
                    serial.UpdatedAt = now;
                }
            }

            var keys = reservation.Items.Select(x => new StockEventKey(x.ProductId, x.ProductVariantId)).Distinct().ToArray();
            if (keys.Length == 0) continue;
            var productIds = keys.Select(x => x.ProductId).Distinct().ToArray();
            var stocks = await db.InventoryStocks.AsNoTracking()
                .Where(x => x.StockLocationId == reservation.StockLocationId && productIds.Contains(x.ProductId))
                .Select(x => new { x.ProductId, x.ProductVariantId, x.QtyOnHand }).ToListAsync(ct);
            var stockMap = stocks.ToDictionary(x => new StockEventKey(x.ProductId, x.ProductVariantId), x => x.QtyOnHand);
            var payload = new StockUpdatedEventV2(
                reservation.StockLocationId,
                reservation.StockLocation.OutletId,
                reservation.StockLocation.Type,
                keys.Select(key => new StockLevelV2(key.ProductId, key.ProductVariantId, stockMap.GetValueOrDefault(key))).ToArray());
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                BusinessId = reservation.BusinessId,
                OutletId = reservation.StockLocation.OutletId,
                EventType = OutboxEventTypes.StockUpdated,
                SchemaVersion = 2,
                PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
                DeduplicationKey = $"stock:reservation:{reservation.Id:N}:expired",
                Status = OutboxMessageStatus.Pending,
                MaxAttempts = _outboxOptions.MaxAttempts,
                OccurredAtUtc = now,
                AvailableAtUtc = now
            });
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
