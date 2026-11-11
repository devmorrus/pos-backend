using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Events;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public sealed class StockReservationService : IStockReservationService
{
    private static readonly TimeSpan ReservationTtl = TimeSpan.FromMinutes(30);
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPosDeviceContext? _device;
    private readonly IIdempotencyService _idempotency;
    private readonly IStockAvailabilityService _availability;
    private readonly IStockEventPublisher _stockEvents;

    public StockReservationService(
        AppDbContext db,
        ICurrentUserService currentUser,
        IIdempotencyService idempotency,
        IStockAvailabilityService availability,
        IStockEventPublisher stockEvents,
        IPosDeviceContext? device = null)
    {
        _db = db;
        _currentUser = currentUser;
        _idempotency = idempotency;
        _availability = availability;
        _stockEvents = stockEvents;
        _device = device;
    }

    public async Task<StockReservationDto> CreateAsync(CreateStockReservationRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User reservasi tidak valid.");
        if (request.Id == Guid.Empty)
            throw new IdempotencyException(IdempotencyErrorCodes.KeyRequired, "Reservation ID wajib tersedia.");

        var session = await _db.CashierSessions.Include(x => x.Register)
            .SingleOrDefaultAsync(x => x.Id == request.CashierSessionId, ct)
            ?? throw new InvalidOperationException("Sesi kasir tidak ditemukan.");
        if (session.Status != CashierSessionStatus.Open || session.OutletId != request.OutletId)
            throw new InvalidOperationException("Sesi kasir tidak aktif atau tidak cocok dengan outlet.");
        if (_currentUser.Role != "Owner" && (session.UserId != userId || _currentUser.OutletId != session.OutletId))
            throw new UnauthorizedAccessException("Anda tidak dapat membuat reservasi untuk sesi kasir tersebut.");
        if (session.DeviceId.HasValue && session.DeviceId != _device?.DeviceId)
            throw new UnauthorizedAccessException("Reservasi harus dibuat dari perangkat sesi kasir.");

        var stockLocationId = session.StockLocationId ?? session.Register?.StockLocationId ?? session.OutletId;
        var normalized = request with
        {
            Items = request.Items
                .OrderBy(x => x.ProductId).ThenBy(x => x.ProductVariantId)
                .Select(x => x with
                {
                    BatchAllocations = x.BatchAllocations?.OrderBy(y => y.ProductBatchId).ToArray(),
                    SerialIds = x.SerialIds?.Distinct().Order().ToArray()
                }).ToArray()
        };
        var hash = _idempotency.ComputeRequestHash(normalized);

        await using var dbTx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var claim = await _idempotency.ClaimAsync(
                IdempotencyOperation.StockReservationCreate, request.Id, hash, userId, request.OutletId, ct);
            if (claim.Replayed && claim.ResourceId.HasValue)
            {
                await dbTx.CommitAsync(ct);
                return await GetByIdAsync(claim.ResourceId.Value, ct);
            }

            var existing = await _db.StockReservations.Include(x => x.Items)
                .SingleOrDefaultAsync(x => x.Id == request.Id, ct);
            if (existing is not null)
            {
                if (!string.Equals(existing.RequestHash, hash, StringComparison.Ordinal))
                    throw new StockIntegrityException(StockIntegrityErrorCodes.ReservationIdReused,
                        "Reservation ID sudah digunakan untuk item yang berbeda.");
                _idempotency.Complete(claim, "stock_reservation", existing.Id);
                await _db.SaveChangesAsync(ct);
                await dbTx.CommitAsync(ct);
                return await GetByIdAsync(existing.Id, ct);
            }

            var now = DateTime.UtcNow;
            var reservation = new StockReservation
            {
                Id = request.Id,
                StockLocationId = stockLocationId,
                CashierSessionId = session.Id,
                RegisterId = session.RegisterId,
                DeviceId = session.DeviceId,
                CreatedBy = userId,
                SourceType = StockReservationSource.Pos,
                Status = StockReservationStatus.Active,
                RequestHash = hash,
                ExpiresAt = now.Add(ReservationTtl),
                CreatedAt = now,
                UpdatedAt = now
            };

            foreach (var itemRequest in normalized.Items)
                await AddItemsAsync(reservation, itemRequest, ct);

            _db.StockReservations.Add(reservation);
            _idempotency.Complete(claim, "stock_reservation", reservation.Id);
            await _db.SaveChangesAsync(ct);
            await _stockEvents.EnqueueCurrentStockAsync(
                stockLocationId,
                reservation.Items.Select(x => new StockEventKey(x.ProductId, x.ProductVariantId)).ToArray(),
                $"stock:reservation:{reservation.Id:N}:active", ct);
            await dbTx.CommitAsync(ct);
            return await GetByIdAsync(reservation.Id, ct);
        }
        catch
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<StockReservationDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var reservation = await ReservationQuery().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Reservasi stok tidak ditemukan.");
        EnsureReadAccess(reservation);
        return Map(reservation);
    }

    public async Task<IReadOnlyList<StockReservationDto>> GetActiveAsync(
        Guid stockLocationId, Guid? productId = null, Guid? productVariantId = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var query = ReservationQuery().AsNoTracking()
            .Where(x => x.StockLocationId == stockLocationId && x.Status == StockReservationStatus.Active && x.ExpiresAt > now);
        if (productId.HasValue)
            query = query.Where(x => x.Items.Any(i => i.ProductId == productId && i.ProductVariantId == productVariantId));
        var rows = await query.OrderBy(x => x.ExpiresAt).ToListAsync(ct);
        foreach (var row in rows) EnsureReadAccess(row);
        return rows.Select(Map).ToList();
    }

    public async Task<StockReservationDto> ReleaseAsync(Guid id, string? reason = null, CancellationToken ct = default)
    {
        await using var dbTx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await LockReservationAsync(id, ct);
            var reservation = await ReservationQuery().SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new KeyNotFoundException("Reservasi stok tidak ditemukan.");
            EnsureWriteAccess(reservation);
            if (reservation.Status == StockReservationStatus.Consumed)
                throw new StockIntegrityException(StockIntegrityErrorCodes.ReservationNotActive,
                    "Reservasi sudah dikonsumsi oleh transaksi.");
            if (reservation.Status is StockReservationStatus.Released or StockReservationStatus.Expired)
            {
                await dbTx.CommitAsync(ct);
                return Map(reservation);
            }

            await ResolveAsync(reservation, reservation.ExpiresAt <= DateTime.UtcNow
                ? StockReservationStatus.Expired : StockReservationStatus.Released, reason, ct);
            await _db.SaveChangesAsync(ct);
            await _stockEvents.EnqueueCurrentStockAsync(
                reservation.StockLocationId,
                reservation.Items.Select(x => new StockEventKey(x.ProductId, x.ProductVariantId)).ToArray(),
                $"stock:reservation:{reservation.Id:N}:{reservation.Status}", ct);
            await dbTx.CommitAsync(ct);
            return Map(reservation);
        }
        catch
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task AddItemsAsync(StockReservation reservation, StockReservationItemRequest request, CancellationToken ct)
    {
        await LockStockAsync(reservation.StockLocationId, request.ProductId, request.ProductVariantId, ct);
        var product = await _db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ProductId && x.IsActive, ct)
            ?? throw new InvalidOperationException("Produk reservasi tidak valid atau tidak aktif.");
        if (product.TrackingMode == ProductTrackingModes.None)
            return;
        if (request.ProductVariantId.HasValue && !await _db.ProductVariants.AnyAsync(x =>
                x.Id == request.ProductVariantId && x.ProductId == product.Id && x.IsActive, ct))
            throw new InvalidOperationException($"Varian produk {product.Name} tidak valid.");

        var onHand = await _db.InventoryStocks.Where(x => x.StockLocationId == reservation.StockLocationId &&
                x.ProductId == product.Id && x.ProductVariantId == request.ProductVariantId)
            .Select(x => (decimal?)x.QtyOnHand).SingleOrDefaultAsync(ct) ?? 0;
        var reserved = await _availability.GetReservedQuantityAsync(
            reservation.StockLocationId, product.Id, request.ProductVariantId, reservation.Id, ct);
        if (onHand - reserved < request.Qty)
            throw new StockIntegrityException(StockIntegrityErrorCodes.ReservationInsufficient,
                $"Stok tersedia {product.Name} tidak mencukupi. Tersedia: {Math.Max(0, onHand - reserved)}, dibutuhkan: {request.Qty}.");

        if (product.TrackingMode == ProductTrackingModes.Quantity)
        {
            if (request.BatchAllocations?.Count > 0 || request.SerialIds?.Count > 0)
                throw new StockIntegrityException(StockIntegrityErrorCodes.ReservationMismatch,
                    "Produk quantity tidak boleh memiliki alokasi batch atau serial.");
            reservation.Items.Add(NewItem(reservation, request, product, request.Qty));
            return;
        }

        if (product.TrackingMode == ProductTrackingModes.Batch)
        {
            var allocations = request.BatchAllocations?.Where(x => x.Qty > 0).ToArray() ?? [];
            if (allocations.Length == 0 || allocations.Sum(x => x.Qty) != request.Qty)
                throw new StockIntegrityException(StockIntegrityErrorCodes.ReservationMismatch,
                    "Total alokasi batch harus sama dengan qty reservasi.");
            foreach (var allocation in allocations.OrderBy(x => x.ProductBatchId))
            {
                await LockBatchAsync(allocation.ProductBatchId, ct);
                var batch = await _db.ProductBatches.SingleOrDefaultAsync(x => x.Id == allocation.ProductBatchId &&
                    x.ProductId == product.Id && x.ProductVariantId == request.ProductVariantId &&
                    x.StockLocationId == reservation.StockLocationId, ct)
                    ?? throw new TrackingValidationException(TrackingErrorCodes.BatchNotFound, "Batch reservasi tidak ditemukan.");
                var batchReserved = await _availability.GetBatchReservedQuantityAsync(batch.Id, reservation.Id, ct);
                if (batch.QtyRemaining - batchReserved < allocation.Qty)
                    throw new StockIntegrityException(StockIntegrityErrorCodes.ReservationInsufficient,
                        $"Stok tersedia batch {batch.BatchNumber} tidak mencukupi.");
                reservation.Items.Add(NewItem(reservation, request, product, allocation.Qty, batch.Id));
            }
            return;
        }

        var serialIds = request.SerialIds?.Distinct().Order().ToArray() ?? [];
        if (request.Qty != decimal.Truncate(request.Qty) || serialIds.Length != request.Qty)
            throw new StockIntegrityException(StockIntegrityErrorCodes.ReservationMismatch,
                "Jumlah serial harus sama dengan qty reservasi.");
        foreach (var serialId in serialIds)
        {
            await LockSerialAsync(serialId, ct);
            var serial = await _db.ProductSerials.SingleOrDefaultAsync(x => x.Id == serialId, ct);
            if (serial is null || serial.ProductId != product.Id || serial.ProductVariantId != request.ProductVariantId ||
                serial.StockLocationId != reservation.StockLocationId ||
                serial.Status is not (ProductSerialStatuses.Available or ProductSerialStatuses.Reserved))
                throw new StockIntegrityException(StockIntegrityErrorCodes.ReservationSerialUnavailable,
                    "Satu atau lebih serial tidak tersedia untuk reservasi.");
            if (serial.Status == ProductSerialStatuses.Reserved)
            {
                var now = DateTime.UtcNow;
                var hasLiveHold = await _db.StockReservationItems.AnyAsync(x =>
                    x.ProductSerialId == serialId && x.Status == StockReservationStatus.Active &&
                    x.StockReservation.Status == StockReservationStatus.Active &&
                    x.StockReservation.ExpiresAt > now, ct);
                if (hasLiveHold)
                    throw new StockIntegrityException(StockIntegrityErrorCodes.ReservationSerialUnavailable,
                        "Satu atau lebih serial tidak tersedia untuk reservasi.");
                // Serial row lock + status is the uniqueness guard; a unique index would block reclaiming expired holds.
            }
            serial.Status = ProductSerialStatuses.Reserved;
            serial.UpdatedAt = DateTime.UtcNow;
            reservation.Items.Add(NewItem(reservation, request, product, 1, productSerialId: serial.Id));
        }
    }

    private static StockReservationItem NewItem(
        StockReservation reservation, StockReservationItemRequest request, Product product, decimal qty,
        Guid? productBatchId = null, Guid? productSerialId = null) => new()
    {
        Id = Guid.NewGuid(),
        StockReservationId = reservation.Id,
        StockLocationId = reservation.StockLocationId,
        ProductId = product.Id,
        ProductVariantId = request.ProductVariantId,
        ProductBatchId = productBatchId,
        ProductSerialId = productSerialId,
        TrackingModeSnapshot = product.TrackingMode,
        Qty = qty,
        Status = StockReservationStatus.Active,
        CreatedAt = DateTime.UtcNow
    };

    private async Task ResolveAsync(StockReservation reservation, string status, string? reason, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        reservation.Status = status;
        reservation.ResolvedAt = now;
        reservation.ReleaseReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        reservation.UpdatedAt = now;
        foreach (var item in reservation.Items)
        {
            item.Status = status;
            item.ResolvedAt = now;
            if (item.ProductSerial is { Status: ProductSerialStatuses.Reserved } serial &&
                !await HasOtherLiveHoldAsync(serial.Id, reservation.Id, now, ct))
            {
                serial.Status = ProductSerialStatuses.Available;
                serial.UpdatedAt = now;
            }
        }
    }

    private Task<bool> HasOtherLiveHoldAsync(Guid serialId, Guid excludingReservationId, DateTime now, CancellationToken ct) =>
        _db.StockReservationItems.AsNoTracking().AnyAsync(x =>
            x.ProductSerialId == serialId && x.StockReservationId != excludingReservationId &&
            x.Status == StockReservationStatus.Active &&
            x.StockReservation.Status == StockReservationStatus.Active &&
            x.StockReservation.ExpiresAt > now, ct);

    private IQueryable<StockReservation> ReservationQuery() => _db.StockReservations
        .Include(x => x.StockLocation).Include(x => x.CreatedByUser)
        .Include(x => x.Items).ThenInclude(x => x.Product)
        .Include(x => x.Items).ThenInclude(x => x.ProductVariant)
        .Include(x => x.Items).ThenInclude(x => x.ProductBatch)
        .Include(x => x.Items).ThenInclude(x => x.ProductSerial);

    private void EnsureReadAccess(StockReservation reservation)
    {
        if (_currentUser.Role == "Owner") return;
        if (_currentUser.OutletId != reservation.StockLocation.OutletId && reservation.CreatedBy != _currentUser.UserId)
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke reservasi tersebut.");
    }

    private void EnsureWriteAccess(StockReservation reservation)
    {
        if (_currentUser.Role is "Owner" or "Admin") return;
        if (reservation.CreatedBy != _currentUser.UserId)
            throw new UnauthorizedAccessException("Anda tidak dapat membatalkan reservasi user lain.");
    }

    private async Task LockReservationAsync(Guid id, CancellationToken ct)
    {
        if (!_db.Database.IsRelational()) return;
        await _db.Database.SqlQuery<int>($"SELECT 1 AS \"Value\" FROM stock_reservations WHERE \"Id\"={id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
    }

    private async Task LockStockAsync(Guid locationId, Guid productId, Guid? variantId, CancellationToken ct)
    {
        if (!_db.Database.IsRelational()) return;
        await _db.Database.SqlQuery<int>($"SELECT 1 AS \"Value\" FROM inventory_stock WHERE \"StockLocationId\"={locationId} AND \"ProductId\"={productId} AND \"ProductVariantId\" IS NOT DISTINCT FROM {variantId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
    }

    private async Task LockBatchAsync(Guid id, CancellationToken ct)
    {
        if (!_db.Database.IsRelational()) return;
        await _db.Database.SqlQuery<int>($"SELECT 1 AS \"Value\" FROM \"ProductBatches\" WHERE \"Id\"={id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
    }

    private async Task LockSerialAsync(Guid id, CancellationToken ct)
    {
        if (!_db.Database.IsRelational()) return;
        await _db.Database.SqlQuery<int>($"SELECT 1 AS \"Value\" FROM product_serials WHERE \"Id\"={id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
    }

    private static StockReservationDto Map(StockReservation x) => new(
        x.Id, x.StockLocationId, x.StockLocation.Name, x.CashierSessionId, x.CreatedBy,
        x.CreatedByUser.Name, x.SourceType, x.Status, x.ExpiresAt, x.ResolvedAt, x.ReleaseReason,
        x.CreatedAt, x.Items.OrderBy(i => i.CreatedAt).Select(i => new StockReservationItemDto(
            i.Id, i.ProductId, i.ProductVariantId, i.Product.Name,
            i.ProductVariant?.Sku ?? i.Product.Sku, i.Qty, i.TrackingModeSnapshot,
            i.ProductBatchId, i.ProductBatch?.BatchNumber, i.ProductSerialId,
            i.ProductSerial?.SerialNumber, i.Status)).ToList());
}
