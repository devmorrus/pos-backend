using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Application.Features.Capabilities;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Application.Common.Events;

namespace MorrusPOS.Infrastructure.Services;

public class StockTransferService : IStockTransferService
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockService;
    private readonly IStockEventPublisher _stockEvents;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICapabilityEvaluator? _capabilityEvaluator;

    public StockTransferService(
        AppDbContext dbContext,
        IStockService stockService,
        IStockEventPublisher stockEvents,
        ICurrentUserService currentUserService,
        ICapabilityEvaluator? capabilityEvaluator = null)
    {
        _dbContext = dbContext;
        _stockService = stockService;
        _stockEvents = stockEvents;
        _currentUserService = currentUserService;
        _capabilityEvaluator = capabilityEvaluator;
    }

    public async Task<StockTransferDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var transfer = await GetAggregateAsync(id, ct);

        if (transfer == null)
        {
            throw new InvalidOperationException("Transfer Stok tidak ditemukan.");
        }

        await EnsureEnabledAsync(transfer.FromOutletId, ct);
        await EnsureEnabledAsync(transfer.ToOutletId, ct);

        return MapToDto(transfer);
    }

    public async Task<IReadOnlyList<StockTransferDto>> GetOutgoingTransfersAsync(Guid outletId, CancellationToken ct = default)
    {
        await EnsureEnabledAsync(outletId, ct);
        var transfers = await TransferQuery()
            .Where(t => t.FromOutletId == outletId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return transfers.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<StockTransferDto>> GetIncomingTransfersAsync(Guid outletId, CancellationToken ct = default)
    {
        await EnsureEnabledAsync(outletId, ct);
        var transfers = await TransferQuery()
            .Where(t => t.ToOutletId == outletId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return transfers.Select(MapToDto).ToList();
    }

    public async Task<StockTransferDto> CreateAsync(Guid userId, CreateStockTransferRequest request, CancellationToken ct = default)
    {
        if (_capabilityEvaluator is not null)
        {
            await _capabilityEvaluator.EnsureEnabledAsync(CapabilityCodes.InventoryTransfer, request.FromOutletId, ct);
            await _capabilityEvaluator.EnsureEnabledAsync(CapabilityCodes.InventoryTransfer, request.ToOutletId, ct);
        }
        if (request.FromOutletId == request.ToOutletId)
        {
            throw new InvalidOperationException("Outlet asal dan tujuan tidak boleh sama.");
        }
        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Item transfer tidak boleh kosong.");
        }

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var transfer = new StockTransfer
            {
                Id = Guid.NewGuid(),
                FromOutletId = request.FromOutletId,
                ToOutletId = request.ToOutletId,
                TransferNumber = $"TRF-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
                Status = StockTransferStatus.Pending,
                RequestedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.StockTransfers.Add(transfer);

            var usedSerialIds = new HashSet<Guid>();

            foreach (var itemReq in request.Items)
            {
                var product = await _dbContext.Products.FindAsync(new object[] { itemReq.ProductId }, ct);
                if (product == null || !product.IsActive)
                {
                    throw new InvalidOperationException("Produk tidak valid.");
                }
                if (product.TrackingMode == ProductTrackingModes.None)
                {
                    throw new TrackingValidationException(TrackingErrorCodes.TrackingModeInvalid,
                        $"Produk '{product.Name}' tidak memiliki stok sehingga tidak dapat ditransfer.");
                }

                switch (product.TrackingMode)
                {
                    case ProductTrackingModes.Quantity:
                        await AddQuantityTransferItemAsync(transfer, product, itemReq, ct);
                        break;
                    case ProductTrackingModes.Batch:
                        await AddBatchTransferItemAsync(transfer, product, itemReq, ct);
                        break;
                    case ProductTrackingModes.Serial:
                        await AddSerialTransferItemsAsync(transfer, product, itemReq, usedSerialIds, ct);
                        break;
                    default:
                        throw new InvalidOperationException($"Tracking mode produk '{product.Name}' tidak valid.");
                }
            }

            await _dbContext.SaveChangesAsync(ct);
            await _stockEvents.EnqueueCurrentStockAsync(
                request.FromOutletId,
                request.Items.Select(item => new StockEventKey(item.ProductId, item.ProductVariantId)).ToArray(),
                $"stock:transfer:{transfer.Id:N}:created",
                ct);
            await dbTx.CommitAsync(ct);

            return await GetByIdAsync(transfer.Id, ct);
        }
        catch
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<StockTransferDto> ApproveAsync(Guid userId, Guid transferId, CancellationToken ct = default)
    {
        var transfer = await GetAggregateAsync(transferId, ct);

        if (transfer == null)
        {
            throw new InvalidOperationException("Transfer Stok tidak ditemukan.");
        }

        await EnsureEnabledAsync(transfer.FromOutletId, ct);
        await EnsureEnabledAsync(transfer.ToOutletId, ct);

        if (transfer.Status != StockTransferStatus.Pending)
        {
            throw new InvalidOperationException("Transfer Stok sudah diproses sebelumnya.");
        }

        EnsureTransferApprovalAccess(transfer.ToOutletId);

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var item in transfer.Items)
            {
                var product = item.Product ?? await _dbContext.Products.FindAsync(new object[] { item.ProductId }, ct)
                    ?? throw new InvalidOperationException("Produk transfer tidak ditemukan.");

                switch (product.TrackingMode)
                {
                    case ProductTrackingModes.Quantity:
                        await _stockService.AddMovementAsync(
                            item.ProductId, transfer.ToOutletId, item.Qty,
                            StockMovementType.TransferIn, "stock_transfer", transfer.Id,
                            $"Transfer masuk dari cabang via {transfer.TransferNumber}", ct,
                            item.ProductVariantId, documentItemId: item.Id);
                        break;
                    case ProductTrackingModes.Batch:
                        await ApproveBatchItemAsync(transfer, item, ct);
                        break;
                    case ProductTrackingModes.Serial:
                        await ApproveSerialItemAsync(transfer, item, ct);
                        break;
                    default:
                        throw new InvalidOperationException($"Tracking mode produk '{product.Name}' tidak valid.");
                }
            }

            transfer.Status = StockTransferStatus.Approved;
            transfer.ApprovedBy = userId;
            transfer.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            await _stockEvents.EnqueueCurrentStockAsync(
                transfer.ToOutletId,
                transfer.Items.Select(item => new StockEventKey(item.ProductId, item.ProductVariantId)).ToArray(),
                $"stock:transfer:{transfer.Id:N}:approved",
                ct);
            await dbTx.CommitAsync(ct);

            return await GetByIdAsync(transfer.Id, ct);
        }
        catch
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<StockTransferDto> RejectAsync(Guid userId, Guid transferId, CancellationToken ct = default)
    {
        var transfer = await GetAggregateAsync(transferId, ct);

        if (transfer == null)
        {
            throw new InvalidOperationException("Transfer Stok tidak ditemukan.");
        }

        await EnsureEnabledAsync(transfer.FromOutletId, ct);
        await EnsureEnabledAsync(transfer.ToOutletId, ct);

        if (transfer.Status != StockTransferStatus.Pending)
        {
            throw new InvalidOperationException("Transfer Stok sudah diproses sebelumnya.");
        }

        EnsureTransferApprovalAccess(transfer.ToOutletId);

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var item in transfer.Items)
            {
                var product = item.Product ?? await _dbContext.Products.FindAsync(new object[] { item.ProductId }, ct)
                    ?? throw new InvalidOperationException("Produk transfer tidak ditemukan.");

                if (product.TrackingMode == ProductTrackingModes.Batch)
                {
                    var batch = item.ProductBatch
                        ?? await _dbContext.ProductBatches.FirstOrDefaultAsync(x => x.Id == item.ProductBatchId, ct)
                        ?? throw new InvalidOperationException("Batch transfer tidak ditemukan.");
                    batch.QtyRemaining += item.Qty;
                    batch.UpdatedAt = DateTime.UtcNow;
                    await _stockService.AddMovementAsync(
                        item.ProductId, transfer.FromOutletId, item.Qty,
                        StockMovementType.TransferIn, "stock_transfer", transfer.Id,
                        $"Pengembalian stok akibat transfer ditolak {transfer.TransferNumber}", ct,
                        item.ProductVariantId, batch.Id, documentItemId: item.Id);
                }
                else if (product.TrackingMode == ProductTrackingModes.Serial)
                {
                    var serial = item.ProductSerial
                        ?? await _dbContext.ProductSerials.FirstOrDefaultAsync(x => x.Id == item.ProductSerialId, ct)
                        ?? throw new InvalidOperationException("Serial transfer tidak ditemukan.");
                    if (serial.Status != ProductSerialStatuses.InTransfer)
                    {
                        throw new InvalidOperationException($"Serial {serial.SerialNumber} tidak sedang dalam transfer.");
                    }
                    serial.Status = ProductSerialStatuses.Available;
                    serial.UpdatedAt = DateTime.UtcNow;
                    await _stockService.AddMovementAsync(
                        item.ProductId, transfer.FromOutletId, 1,
                        StockMovementType.TransferIn, "stock_transfer", transfer.Id,
                        $"Pengembalian stok akibat transfer ditolak {transfer.TransferNumber}", ct,
                        item.ProductVariantId, productSerialId: serial.Id, documentItemId: item.Id);
                }
                else
                {
                    await _stockService.AddMovementAsync(
                        item.ProductId, transfer.FromOutletId, item.Qty,
                        StockMovementType.TransferIn, "stock_transfer", transfer.Id,
                        $"Pengembalian stok akibat transfer ditolak {transfer.TransferNumber}", ct,
                        item.ProductVariantId, documentItemId: item.Id);
                }
            }

            transfer.Status = StockTransferStatus.Rejected;
            transfer.ApprovedBy = userId;
            transfer.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            await _stockEvents.EnqueueCurrentStockAsync(
                transfer.FromOutletId,
                transfer.Items.Select(item => new StockEventKey(item.ProductId, item.ProductVariantId)).ToArray(),
                $"stock:transfer:{transfer.Id:N}:rejected",
                ct);
            await dbTx.CommitAsync(ct);

            return await GetByIdAsync(transfer.Id, ct);
        }
        catch
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task AddQuantityTransferItemAsync(
        StockTransfer transfer, Product product, StockTransferItemRequest itemReq, CancellationToken ct)
    {
        if (itemReq.Qty <= 0)
        {
            throw new InvalidOperationException($"Qty transfer untuk {product.Name} harus lebih dari 0.");
        }

        var stock = await _dbContext.InventoryStocks
            .FirstOrDefaultAsync(s => s.ProductId == itemReq.ProductId &&
                s.ProductVariantId == itemReq.ProductVariantId &&
                s.OutletId == transfer.FromOutletId, ct);

        if (stock == null || stock.QtyOnHand < itemReq.Qty)
        {
            throw new InvalidOperationException($"Stok tidak mencukupi di outlet asal untuk produk {product.Name}. (Stok tersedia: {stock?.QtyOnHand ?? 0}, Dibutuhkan: {itemReq.Qty})");
        }

        var item = new StockTransferItem
        {
            Id = Guid.NewGuid(),
            StockTransferId = transfer.Id,
            ProductId = itemReq.ProductId,
            ProductVariantId = itemReq.ProductVariantId,
            Qty = itemReq.Qty
        };
        _dbContext.StockTransferItems.Add(item);

        await _stockService.AddMovementAsync(
            itemReq.ProductId, transfer.FromOutletId, -itemReq.Qty,
            StockMovementType.TransferOut, "stock_transfer", transfer.Id,
            $"Transfer keluar ke cabang via {transfer.TransferNumber}", ct,
            itemReq.ProductVariantId, documentItemId: item.Id);
    }

    private async Task AddBatchTransferItemAsync(
        StockTransfer transfer, Product product, StockTransferItemRequest itemReq, CancellationToken ct)
    {
        if (itemReq.Qty <= 0)
        {
            throw new InvalidOperationException($"Qty transfer batch {product.Name} harus lebih dari 0.");
        }
        if (!itemReq.ProductBatchId.HasValue)
        {
            throw new TrackingValidationException(TrackingErrorCodes.BatchRequired,
                $"Batch wajib dipilih untuk transfer produk {product.Name}.");
        }

        var batch = await _dbContext.ProductBatches.FirstOrDefaultAsync(x =>
            x.Id == itemReq.ProductBatchId.Value &&
            x.ProductId == itemReq.ProductId &&
            x.ProductVariantId == itemReq.ProductVariantId &&
            x.OutletId == transfer.FromOutletId, ct)
            ?? throw new TrackingValidationException(TrackingErrorCodes.BatchNotFound,
                $"Batch untuk produk {product.Name} tidak ditemukan di outlet asal.");

        if (batch.QtyRemaining < itemReq.Qty)
        {
            throw new TrackingValidationException(TrackingErrorCodes.BatchStockInsufficient,
                $"Stok batch {batch.BatchNumber} tidak mencukupi. (Tersedia: {batch.QtyRemaining}, Dibutuhkan: {itemReq.Qty})");
        }

        batch.QtyRemaining -= itemReq.Qty;
        batch.UpdatedAt = DateTime.UtcNow;

        var item = new StockTransferItem
        {
            Id = Guid.NewGuid(),
            StockTransferId = transfer.Id,
            ProductId = itemReq.ProductId,
            ProductVariantId = itemReq.ProductVariantId,
            ProductBatchId = batch.Id,
            Qty = itemReq.Qty
        };
        _dbContext.StockTransferItems.Add(item);

        await _stockService.AddMovementAsync(
            itemReq.ProductId, transfer.FromOutletId, -itemReq.Qty,
            StockMovementType.TransferOut, "stock_transfer", transfer.Id,
            $"Transfer keluar ke cabang via {transfer.TransferNumber}", ct,
            itemReq.ProductVariantId, batch.Id, documentItemId: item.Id);
    }

    private async Task AddSerialTransferItemsAsync(
        StockTransfer transfer, Product product, StockTransferItemRequest itemReq,
        HashSet<Guid> usedSerialIds, CancellationToken ct)
    {
        var serialIds = itemReq.ProductSerialIds?.Distinct().ToList() ?? [];
        if (serialIds.Count == 0)
        {
            throw new TrackingValidationException(TrackingErrorCodes.SerialRequired,
                $"Serial wajib dipilih untuk transfer produk {product.Name}.");
        }
        if (serialIds.Count != serialIds.Distinct().Count() || serialIds.Any(x => !usedSerialIds.Add(x)))
        {
            throw new TrackingValidationException(TrackingErrorCodes.SerialDuplicate,
                "Serial yang sama tidak boleh dikirim dua kali.");
        }

        var serials = await _dbContext.ProductSerials
            .Where(x => serialIds.Contains(x.Id))
            .ToListAsync(ct);

        if (serials.Count != serialIds.Count)
        {
            throw new TrackingValidationException(TrackingErrorCodes.SerialNotAvailable,
                $"Satu atau lebih serial produk {product.Name} tidak ditemukan.");
        }

        foreach (var serial in serials)
        {
            if (serial.ProductId != itemReq.ProductId ||
                serial.ProductVariantId != itemReq.ProductVariantId ||
                serial.OutletId != transfer.FromOutletId ||
                serial.Status != ProductSerialStatuses.Available)
            {
                throw new TrackingValidationException(TrackingErrorCodes.SerialNotAvailable,
                    $"Serial {serial.SerialNumber} tidak tersedia untuk transfer.");
            }

            serial.Status = ProductSerialStatuses.InTransfer;
            serial.UpdatedAt = DateTime.UtcNow;

            var item = new StockTransferItem
            {
                Id = Guid.NewGuid(),
                StockTransferId = transfer.Id,
                ProductId = itemReq.ProductId,
                ProductVariantId = itemReq.ProductVariantId,
                ProductSerialId = serial.Id,
                Qty = 1
            };
            _dbContext.StockTransferItems.Add(item);

            await _stockService.AddMovementAsync(
                itemReq.ProductId, transfer.FromOutletId, -1,
                StockMovementType.TransferOut, "stock_transfer", transfer.Id,
                $"Transfer keluar ke cabang via {transfer.TransferNumber}", ct,
                itemReq.ProductVariantId, productSerialId: serial.Id, documentItemId: item.Id);
        }
    }

    private async Task ApproveBatchItemAsync(StockTransfer transfer, StockTransferItem item, CancellationToken ct)
    {
        var sourceBatch = item.ProductBatch
            ?? await _dbContext.ProductBatches.FirstOrDefaultAsync(x => x.Id == item.ProductBatchId, ct)
            ?? throw new InvalidOperationException("Batch transfer tidak ditemukan.");

        var destinationBatch = await _dbContext.ProductBatches.FirstOrDefaultAsync(x =>
            x.OutletId == transfer.ToOutletId &&
            x.ProductId == item.ProductId &&
            x.ProductVariantId == item.ProductVariantId &&
            x.BatchNumber == sourceBatch.BatchNumber, ct);

        if (destinationBatch == null)
        {
            destinationBatch = new ProductBatch
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                ProductVariantId = item.ProductVariantId,
                OutletId = transfer.ToOutletId,
                BatchNumber = sourceBatch.BatchNumber,
                ExpiryDate = sourceBatch.ExpiryDate,
                QtyProduction = item.Qty,
                QtyRemaining = item.Qty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.ProductBatches.Add(destinationBatch);
        }
        else
        {
            if (destinationBatch.ExpiryDate?.Date != sourceBatch.ExpiryDate?.Date)
            {
                throw new InvalidOperationException($"Expiry batch {sourceBatch.BatchNumber} berbeda di outlet tujuan.");
            }
            destinationBatch.QtyProduction += item.Qty;
            destinationBatch.QtyRemaining += item.Qty;
            destinationBatch.UpdatedAt = DateTime.UtcNow;
        }

        await _stockService.AddMovementAsync(
            item.ProductId, transfer.ToOutletId, item.Qty,
            StockMovementType.TransferIn, "stock_transfer", transfer.Id,
            $"Transfer masuk dari cabang via {transfer.TransferNumber}", ct,
            item.ProductVariantId, destinationBatch.Id, documentItemId: item.Id);
    }

    private async Task ApproveSerialItemAsync(StockTransfer transfer, StockTransferItem item, CancellationToken ct)
    {
        var serial = item.ProductSerial
            ?? await _dbContext.ProductSerials.FirstOrDefaultAsync(x => x.Id == item.ProductSerialId, ct)
            ?? throw new InvalidOperationException("Serial transfer tidak ditemukan.");

        if (serial.Status != ProductSerialStatuses.InTransfer)
        {
            throw new InvalidOperationException($"Serial {serial.SerialNumber} tidak sedang dalam transfer.");
        }

        serial.OutletId = transfer.ToOutletId;
        serial.Status = ProductSerialStatuses.Available;
        serial.UpdatedAt = DateTime.UtcNow;

        await _stockService.AddMovementAsync(
            item.ProductId, transfer.ToOutletId, 1,
            StockMovementType.TransferIn, "stock_transfer", transfer.Id,
            $"Transfer masuk dari cabang via {transfer.TransferNumber}", ct,
            item.ProductVariantId, productSerialId: serial.Id, documentItemId: item.Id);
    }

    private IQueryable<StockTransfer> TransferQuery(bool tracking = false)
    {
        var query = _dbContext.StockTransfers
            .Include(t => t.FromOutlet)
            .Include(t => t.ToOutlet)
            .Include(t => t.RequestedByUser)
            .Include(t => t.ApprovedByUser)
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .Include(t => t.Items).ThenInclude(i => i.ProductBatch)
            .Include(t => t.Items).ThenInclude(i => i.ProductSerial);
        return tracking ? query : query.AsNoTracking();
    }

    private Task<StockTransfer?> GetAggregateAsync(Guid id, CancellationToken ct)
        => TransferQuery(tracking: true).FirstOrDefaultAsync(t => t.Id == id, ct);

    private static StockTransferDto MapToDto(StockTransfer t)
    {
        return new StockTransferDto(
            t.Id,
            t.FromOutletId,
            t.FromOutlet?.Name ?? string.Empty,
            t.ToOutletId,
            t.ToOutlet?.Name ?? string.Empty,
            t.TransferNumber,
            t.Status,
            t.RequestedBy,
            t.RequestedByUser?.Name ?? string.Empty,
            t.ApprovedBy,
            t.ApprovedByUser?.Name,
            t.CreatedAt,
            t.Items.Select(i => new StockTransferItemDto(
                i.ProductId,
                i.Product?.Name ?? string.Empty,
                i.ProductVariant?.Sku ?? i.Product?.Sku ?? string.Empty,
                i.Qty
            )
            {
                ProductVariantId = i.ProductVariantId,
                ProductBatchId = i.ProductBatchId,
                BatchNumber = i.ProductBatch?.BatchNumber,
                ExpiryDate = i.ProductBatch?.ExpiryDate,
                ProductSerialId = i.ProductSerialId,
                SerialNumber = i.ProductSerial?.SerialNumber
            }).ToList()
        );
    }

    private void EnsureTransferApprovalAccess(Guid targetOutletId)
    {
        if (_currentUserService.Role is "Owner" or "Admin")
        {
            return;
        }

        if (_currentUserService.OutletId != targetOutletId)
        {
            throw new UnauthorizedAccessException("Approve atau reject transfer hanya bisa dilakukan oleh outlet tujuan.");
        }
    }

    private Task EnsureEnabledAsync(Guid outletId, CancellationToken ct)
        => _capabilityEvaluator?.EnsureEnabledAsync(CapabilityCodes.InventoryTransfer, outletId, ct)
            ?? Task.CompletedTask;
}
