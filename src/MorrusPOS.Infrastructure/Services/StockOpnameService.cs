using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Capabilities;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class StockOpnameService : IStockOpnameService
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockService;
    private readonly IPosNotificationService _notificationService;
    private readonly ICapabilityEvaluator? _capabilities;

    public StockOpnameService(
        AppDbContext dbContext,
        IStockService stockService,
        IPosNotificationService notificationService,
        ICapabilityEvaluator? capabilities = null)
    {
        _dbContext = dbContext;
        _stockService = stockService;
        _notificationService = notificationService;
        _capabilities = capabilities;
    }

    public async Task<StockOpnameDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var opname = await OpnameQuery()
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (opname == null)
        {
            throw new InvalidOperationException("Laporan Stok Opname tidak ditemukan.");
        }

        await EnsureEnabledAsync(opname.OutletId, ct);

        return MapToDto(opname);
    }

    public async Task<IReadOnlyList<StockOpnameDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default)
    {
        await EnsureEnabledAsync(outletId, ct);
        var opnames = await OpnameQuery()
            .Where(o => o.OutletId == outletId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return opnames.Select(MapToDto).ToList();
    }

    public async Task<StockOpnameDto> CreateAsync(Guid userId, CreateStockOpnameRequest request, CancellationToken ct = default)
    {
        await EnsureEnabledAsync(request.OutletId, ct);
        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Item opname tidak boleh kosong.");
        }

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
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
            var seenRows = new HashSet<(Guid ProductId, Guid? VariantId, Guid? BatchId)>();
            var seenSerialIds = new HashSet<Guid>();

            foreach (var itemReq in request.Items)
            {
                if (itemReq.PhysicalQty < 0)
                {
                    throw new InvalidOperationException("Jumlah fisik tidak boleh negatif.");
                }

                var product = await _dbContext.Products.FindAsync(new object[] { itemReq.ProductId }, ct);
                if (product == null || !product.IsActive)
                {
                    throw new InvalidOperationException("Produk tidak valid.");
                }
                if (itemReq.ProductVariantId.HasValue &&
                    !await _dbContext.ProductVariants.AnyAsync(v =>
                        v.Id == itemReq.ProductVariantId.Value && v.ProductId == product.Id, ct))
                {
                    throw new InvalidOperationException($"Varian produk '{product.Name}' tidak valid.");
                }
                if (!seenRows.Add((itemReq.ProductId, itemReq.ProductVariantId, itemReq.ProductBatchId)))
                {
                    throw new TrackingValidationException(TrackingErrorCodes.TrackingAllocationMismatch,
                        $"Baris opname produk '{product.Name}' duplikat.");
                }

                switch (product.TrackingMode)
                {
                    case ProductTrackingModes.None:
                        throw new TrackingValidationException(TrackingErrorCodes.TrackingModeInvalid,
                            $"Produk '{product.Name}' tidak memiliki stok sehingga tidak perlu diopname.");
                    case ProductTrackingModes.Quantity:
                        await AddQuantityItemAsync(opname, product, itemReq, request.OutletId, stockUpdates, ct);
                        break;
                    case ProductTrackingModes.Batch:
                        await AddBatchItemAsync(opname, product, itemReq, request.OutletId, stockUpdates, ct);
                        break;
                    case ProductTrackingModes.Serial:
                        await AddSerialItemsAsync(opname, product, itemReq, request.OutletId, stockUpdates, seenSerialIds, ct);
                        break;
                    default:
                        throw new InvalidOperationException($"Tracking mode produk '{product.Name}' tidak valid.");
                }
            }

            await _dbContext.SaveChangesAsync(ct);
            await dbTx.CommitAsync(ct);

            // Broadcast changes via SignalR
            if (stockUpdates.Count > 0)
            {
                await _notificationService.SendStockUpdateAsync(request.OutletId, stockUpdates, ct);
            }

            return await GetByIdAsync(opname.Id, ct);
        }
        catch
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task AddQuantityItemAsync(
        StockOpname opname,
        Product product,
        StockOpnameItemRequest itemReq,
        Guid outletId,
        List<StockUpdateItem> stockUpdates,
        CancellationToken ct)
    {
        if (itemReq.ProductBatchId.HasValue || itemReq.ProductSerialIds is { Count: > 0 })
        {
            throw new TrackingValidationException(TrackingErrorCodes.TrackingModeInvalid,
                $"Produk '{product.Name}' tidak memakai batch atau serial.");
        }

        var stock = await _dbContext.InventoryStocks
            .FirstOrDefaultAsync(s => s.ProductId == itemReq.ProductId &&
                s.ProductVariantId == itemReq.ProductVariantId &&
                s.OutletId == outletId, ct);

        var systemQty = stock?.QtyOnHand ?? 0;
        var variance = itemReq.PhysicalQty - systemQty;

        var opnameItem = NewOpnameItem(opname, itemReq, systemQty, itemReq.PhysicalQty, variance);
        _dbContext.StockOpnameItems.Add(opnameItem);

        if (variance != 0)
        {
            await _stockService.AddMovementAsync(
                productId: itemReq.ProductId,
                outletId: outletId,
                qtyChange: variance,
                movementType: StockMovementType.OpnameAdjustment,
                referenceType: "stock_opname",
                referenceId: opname.Id,
                note: $"Penyesuaian stok opname fisik {itemReq.PhysicalQty} vs sistem {systemQty}",
                ct: ct,
                productVariantId: itemReq.ProductVariantId
            );

            stockUpdates.Add(new StockUpdateItem(itemReq.ProductId, variance, itemReq.ProductVariantId));
        }
    }

    private async Task AddBatchItemAsync(
        StockOpname opname,
        Product product,
        StockOpnameItemRequest itemReq,
        Guid outletId,
        List<StockUpdateItem> stockUpdates,
        CancellationToken ct)
    {
        if (!itemReq.ProductBatchId.HasValue)
        {
            throw new TrackingValidationException(TrackingErrorCodes.BatchRequired,
                $"Batch wajib dipilih untuk opname produk '{product.Name}'.");
        }
        if (itemReq.ProductSerialIds is { Count: > 0 })
        {
            throw new TrackingValidationException(TrackingErrorCodes.TrackingModeInvalid,
                $"Produk '{product.Name}' memakai batch, bukan serial.");
        }

        var batch = await _dbContext.ProductBatches.FirstOrDefaultAsync(x =>
            x.Id == itemReq.ProductBatchId.Value &&
            x.ProductId == itemReq.ProductId &&
            x.ProductVariantId == itemReq.ProductVariantId &&
            x.OutletId == outletId, ct);

        if (batch == null)
        {
            throw new TrackingValidationException(TrackingErrorCodes.BatchNotFound,
                $"Batch produk '{product.Name}' tidak ditemukan di outlet ini.");
        }

        var systemQty = batch.QtyRemaining;
        var variance = itemReq.PhysicalQty - systemQty;

        var opnameItem = NewOpnameItem(opname, itemReq, systemQty, itemReq.PhysicalQty, variance);
        opnameItem.ProductBatchId = batch.Id;
        _dbContext.StockOpnameItems.Add(opnameItem);

        if (variance == 0)
        {
            return;
        }

        // Opname can find more than was ever recorded, so production qty may need to grow.
        if (variance > 0)
        {
            batch.QtyProduction = Math.Max(batch.QtyProduction, itemReq.PhysicalQty);
        }
        batch.QtyRemaining = itemReq.PhysicalQty;
        batch.UpdatedAt = DateTime.UtcNow;

        await _stockService.AddMovementAsync(
            productId: itemReq.ProductId,
            outletId: outletId,
            qtyChange: variance,
            movementType: StockMovementType.OpnameAdjustment,
            referenceType: "stock_opname",
            referenceId: opname.Id,
            note: $"Penyesuaian opname batch {batch.BatchNumber}: fisik {itemReq.PhysicalQty} vs sistem {systemQty}",
            ct: ct,
            productVariantId: itemReq.ProductVariantId,
            productBatchId: batch.Id
        );

        stockUpdates.Add(new StockUpdateItem(itemReq.ProductId, variance, itemReq.ProductVariantId));
    }

    private async Task AddSerialItemsAsync(
        StockOpname opname,
        Product product,
        StockOpnameItemRequest itemReq,
        Guid outletId,
        List<StockUpdateItem> stockUpdates,
        HashSet<Guid> seenSerialIds,
        CancellationToken ct)
    {
        if (itemReq.ProductSerialIds is null)
        {
            throw new TrackingValidationException(TrackingErrorCodes.SerialRequired,
                $"Daftar serial hasil hitung wajib dikirim untuk opname produk '{product.Name}'.");
        }
        if (itemReq.ProductBatchId.HasValue)
        {
            throw new TrackingValidationException(TrackingErrorCodes.TrackingModeInvalid,
                $"Produk '{product.Name}' memakai serial, bukan batch.");
        }

        var scannedIds = itemReq.ProductSerialIds.ToList();
        if (scannedIds.Count != scannedIds.Distinct().Count() || scannedIds.Any(id => !seenSerialIds.Add(id)))
        {
            throw new TrackingValidationException(TrackingErrorCodes.SerialDuplicate,
                $"Serial produk '{product.Name}' tidak boleh dikirim dua kali.");
        }

        var available = await _dbContext.ProductSerials
            .Where(x => x.ProductId == itemReq.ProductId &&
                x.ProductVariantId == itemReq.ProductVariantId &&
                x.OutletId == outletId &&
                x.Status == ProductSerialStatuses.Available)
            .ToListAsync(ct);

        var scanned = available.Where(x => scannedIds.Contains(x.Id)).ToList();
        if (scanned.Count != scannedIds.Count)
        {
            throw new TrackingValidationException(TrackingErrorCodes.SerialNotAvailable,
                $"Satu atau lebih serial produk '{product.Name}' tidak tersedia di outlet ini.");
        }

        var scannedSet = scannedIds.ToHashSet();
        foreach (var serial in available)
        {
            var present = scannedSet.Contains(serial.Id);
            var opnameItem = new StockOpnameItem
            {
                Id = Guid.NewGuid(),
                StockOpnameId = opname.Id,
                ProductId = itemReq.ProductId,
                ProductVariantId = itemReq.ProductVariantId,
                ProductSerialId = serial.Id,
                SystemQty = 1,
                PhysicalQty = present ? 1 : 0,
                Variance = present ? 0 : -1
            };
            _dbContext.StockOpnameItems.Add(opnameItem);

            if (present)
            {
                continue;
            }

            serial.Status = ProductSerialStatuses.WrittenOff;
            serial.UpdatedAt = DateTime.UtcNow;

            await _stockService.AddMovementAsync(
                productId: itemReq.ProductId,
                outletId: outletId,
                qtyChange: -1,
                movementType: StockMovementType.OpnameAdjustment,
                referenceType: "stock_opname",
                referenceId: opname.Id,
                note: $"Serial {serial.SerialNumber} tidak ditemukan saat opname",
                ct: ct,
                productVariantId: itemReq.ProductVariantId,
                productSerialId: serial.Id
            );

            stockUpdates.Add(new StockUpdateItem(itemReq.ProductId, -1, itemReq.ProductVariantId));
        }
    }

    private static StockOpnameItem NewOpnameItem(
        StockOpname opname,
        StockOpnameItemRequest itemReq,
        decimal systemQty,
        decimal physicalQty,
        decimal variance)
    {
        return new StockOpnameItem
        {
            Id = Guid.NewGuid(),
            StockOpnameId = opname.Id,
            ProductId = itemReq.ProductId,
            ProductVariantId = itemReq.ProductVariantId,
            SystemQty = systemQty,
            PhysicalQty = physicalQty,
            Variance = variance
        };
    }

    private IQueryable<StockOpname> OpnameQuery()
        => _dbContext.StockOpnames
            .Include(o => o.Outlet)
            .Include(o => o.PerformedByUser)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Items).ThenInclude(i => i.ProductVariant)
            .Include(o => o.Items).ThenInclude(i => i.ProductBatch)
            .Include(o => o.Items).ThenInclude(i => i.ProductSerial)
            .AsNoTracking();

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
                i.ProductVariant?.Sku ?? i.Product?.Sku ?? string.Empty,
                i.SystemQty,
                i.PhysicalQty,
                i.Variance
            )
            {
                ProductVariantId = i.ProductVariantId,
                ProductBatchId = i.ProductBatchId,
                BatchNumber = i.ProductBatch?.BatchNumber,
                ProductSerialId = i.ProductSerialId,
                SerialNumber = i.ProductSerial?.SerialNumber
            }).ToList()
        );
    }

    private Task EnsureEnabledAsync(Guid outletId, CancellationToken ct)
        => _capabilities?.EnsureEnabledAsync(CapabilityCodes.InventoryOpname, outletId, ct)
            ?? Task.CompletedTask;
}
