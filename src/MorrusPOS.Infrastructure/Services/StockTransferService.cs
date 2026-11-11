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
    private readonly IIdempotencyService? _idempotencyService;

    public StockTransferService(
        AppDbContext dbContext,
        IStockService stockService,
        IStockEventPublisher stockEvents,
        ICurrentUserService currentUserService,
        ICapabilityEvaluator? capabilityEvaluator = null,
        IIdempotencyService? idempotencyService = null)
    {
        _dbContext = dbContext;
        _stockService = stockService;
        _stockEvents = stockEvents;
        _currentUserService = currentUserService;
        _capabilityEvaluator = capabilityEvaluator;
        _idempotencyService = idempotencyService;
    }

    public async Task<StockTransferDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var transfer = await GetAggregateAsync(id, ct);

        if (transfer == null)
        {
            throw new InvalidOperationException("Transfer Stok tidak ditemukan.");
        }

        var hasSourceAccess = await HasLocationAccessAsync(transfer.FromStockLocationId, ct);
        var hasDestinationAccess = await HasLocationAccessAsync(transfer.ToStockLocationId, ct);
        if (!hasSourceAccess && !hasDestinationAccess)
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke transfer stok tersebut.");
        await EnsureEnabledAsync(hasDestinationAccess ? transfer.ToStockLocationId : transfer.FromStockLocationId, ct);

        return MapToDto(transfer);
    }

    public async Task<IReadOnlyList<StockTransferDto>> GetOutgoingTransfersAsync(Guid stockLocationId, CancellationToken ct = default)
    {
        await EnsureEnabledAsync(stockLocationId, ct);
        await EnsureLocationAccessAsync(stockLocationId, ct);
        var transfers = await TransferQuery()
            .Where(t => t.FromStockLocationId == stockLocationId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return transfers.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<StockTransferDto>> GetIncomingTransfersAsync(Guid stockLocationId, CancellationToken ct = default)
    {
        await EnsureEnabledAsync(stockLocationId, ct);
        await EnsureLocationAccessAsync(stockLocationId, ct);
        var transfers = await TransferQuery()
            .Where(t => t.ToStockLocationId == stockLocationId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return transfers.Select(MapToDto).ToList();
    }

    public async Task<StockTransferDto> CreateAsync(Guid userId, CreateStockTransferRequest request, CancellationToken ct = default)
    {
        await EnsureEnabledAsync(request.FromStockLocationId, ct);
        await EnsureEnabledAsync(request.ToStockLocationId, ct);
        await EnsureLocationAccessAsync(request.FromStockLocationId, ct);
        if (request.FromStockLocationId == request.ToStockLocationId)
        {
            throw new InvalidOperationException("Lokasi asal dan tujuan tidak boleh sama.");
        }
        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Item transfer tidak boleh kosong.");
        }

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            IdempotencyClaim? claim = null;
            if (_idempotencyService is not null && request.RequestId != Guid.Empty)
            {
                claim = await _idempotencyService.ClaimAsync(
                    IdempotencyOperation.StockTransferSend,
                    request.RequestId,
                    _idempotencyService.ComputeRequestHash(request),
                    userId,
                    _currentUserService.OutletId,
                    ct);
                if (claim.Replayed && claim.ResourceId.HasValue)
                {
                    await dbTx.CommitAsync(ct);
                    return await GetByIdAsync(claim.ResourceId.Value, ct);
                }
            }

            var transfer = new StockTransfer
            {
                Id = Guid.NewGuid(),
                FromStockLocationId = request.FromStockLocationId,
                ToStockLocationId = request.ToStockLocationId,
                TransferNumber = $"TRF-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
                Status = StockTransferStatus.InTransit,
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
                var unitCost = product.CostPrice;
                if (itemReq.ProductVariantId.HasValue)
                {
                    var variant = await _dbContext.ProductVariants.AsNoTracking().SingleOrDefaultAsync(x =>
                        x.Id == itemReq.ProductVariantId && x.ProductId == product.Id && x.IsActive, ct)
                        ?? throw new InvalidOperationException($"Varian produk '{product.Name}' tidak valid.");
                    unitCost = variant.CostPrice;
                }

                switch (product.TrackingMode)
                {
                    case ProductTrackingModes.Quantity:
                        await AddQuantityTransferItemAsync(transfer, product, itemReq, unitCost, ct);
                        break;
                    case ProductTrackingModes.Batch:
                        await AddBatchTransferItemAsync(transfer, product, itemReq, unitCost, ct);
                        break;
                    case ProductTrackingModes.Serial:
                        await AddSerialTransferItemsAsync(transfer, product, itemReq, usedSerialIds, unitCost, ct);
                        break;
                    default:
                        throw new InvalidOperationException($"Tracking mode produk '{product.Name}' tidak valid.");
                }
            }

            if (claim is not null)
                _idempotencyService!.Complete(claim, "stock_transfer", transfer.Id);
            await _dbContext.SaveChangesAsync(ct);
            await _stockEvents.EnqueueCurrentStockAsync(
                request.FromStockLocationId,
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

    public async Task<StockTransferDto> ReceiveAsync(
        Guid userId, Guid transferId, ReceiveStockTransferRequest request, CancellationToken ct = default)
    {
        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            if (_dbContext.Database.IsRelational())
            {
                await _dbContext.Database.SqlQuery<int>(
                    $"SELECT 1 AS \"Value\" FROM stock_transfers WHERE \"Id\" = {transferId} FOR UPDATE")
                    .SingleOrDefaultAsync(ct);
            }

            var transfer = await GetAggregateAsync(transferId, ct)
                ?? throw new KeyNotFoundException("Transfer stok tidak ditemukan.");
            await EnsureEnabledAsync(transfer.ToStockLocationId, ct);
            await EnsureTransferApprovalAccessAsync(transfer.ToStockLocationId, ct);

            IdempotencyClaim? claim = null;
            if (_idempotencyService is not null && request.RequestId != Guid.Empty)
            {
                claim = await _idempotencyService.ClaimAsync(
                    IdempotencyOperation.StockTransferReceive,
                    request.RequestId,
                    _idempotencyService.ComputeRequestHash(new { transferId, request }),
                    userId,
                    transfer.ToStockLocation?.OutletId,
                    ct);
                if (claim.Replayed && claim.ResourceId.HasValue)
                {
                    await dbTx.CommitAsync(ct);
                    return await GetByIdAsync(claim.ResourceId.Value, ct);
                }
            }

            if (transfer.Status is not (StockTransferStatus.InTransit or StockTransferStatus.Pending))
                throw new StockIntegrityException(StockIntegrityErrorCodes.TransferAlreadyReceived,
                    "Transfer stok sudah diproses atau tidak lagi dalam perjalanan.");

            if (request.Items.Count != transfer.Items.Count ||
                request.Items.Select(x => x.TransferItemId).Distinct().Count() != request.Items.Count ||
                request.Items.Any(x => transfer.Items.All(item => item.Id != x.TransferItemId)))
                throw new StockIntegrityException(StockIntegrityErrorCodes.TransferItemMismatch,
                    "Semua item transfer harus diperiksa tepat satu kali.");

            var hasVariance = false;
            var changedStock = new List<StockEventKey>();
            foreach (var item in transfer.Items)
            {
                var receipt = request.Items.Single(x => x.TransferItemId == item.Id);
                if (receipt.ReceivedQty < 0)
                    throw new StockIntegrityException(StockIntegrityErrorCodes.TransferItemMismatch,
                        "Qty diterima tidak boleh negatif.");

                var difference = receipt.ReceivedQty - item.Qty;
                var reason = string.IsNullOrWhiteSpace(receipt.VarianceReasonCode)
                    ? null : receipt.VarianceReasonCode.Trim().ToLowerInvariant();
                var note = string.IsNullOrWhiteSpace(receipt.VarianceNote) ? null : receipt.VarianceNote.Trim();
                ValidateVariance(difference, reason, note);

                var product = item.Product ?? await _dbContext.Products.FindAsync([item.ProductId], ct)
                    ?? throw new InvalidOperationException("Produk transfer tidak ditemukan.");

                switch (product.TrackingMode)
                {
                    case ProductTrackingModes.Quantity:
                        if (receipt.ReceivedQty > 0)
                            await AddReceivedMovementAsync(transfer, item, receipt.ReceivedQty, null, null, reason, ct);
                        break;
                    case ProductTrackingModes.Batch:
                        await ReceiveBatchItemAsync(transfer, item, receipt.ReceivedQty, reason, ct);
                        break;
                    case ProductTrackingModes.Serial:
                        await ReceiveSerialItemAsync(transfer, item, receipt.ReceivedQty, reason, ct);
                        break;
                    default:
                        throw new InvalidOperationException($"Tracking mode produk '{product.Name}' tidak valid.");
                }

                item.ReceivedQty = receipt.ReceivedQty;
                item.DifferenceQty = difference;
                item.VarianceReasonCode = difference == 0 ? null : reason;
                item.VarianceNote = difference == 0 ? null : note;
                hasVariance |= difference != 0;
                if (receipt.ReceivedQty > 0)
                    changedStock.Add(new StockEventKey(item.ProductId, item.ProductVariantId));
            }

            transfer.Status = hasVariance ? StockTransferStatus.ReceivedWithVariance : StockTransferStatus.Received;
            transfer.ApprovedBy = userId;
            transfer.ReceivedAt = DateTime.UtcNow;
            transfer.ReceiptNote = string.IsNullOrWhiteSpace(request.ReceiptNote) ? null : request.ReceiptNote.Trim();
            transfer.UpdatedAt = DateTime.UtcNow;

            if (claim is not null)
                _idempotencyService!.Complete(claim, "stock_transfer", transfer.Id);
            await _dbContext.SaveChangesAsync(ct);
            await _stockEvents.EnqueueCurrentStockAsync(
                transfer.ToStockLocationId,
                changedStock,
                $"stock:transfer:{transfer.Id:N}:received",
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
        var transfer = await GetAggregateAsync(transferId, ct)
            ?? throw new KeyNotFoundException("Transfer stok tidak ditemukan.");
        return await ReceiveAsync(userId, transferId, new ReceiveStockTransferRequest(
            Guid.Empty,
            transfer.Items.Select(x => new ReceiveStockTransferItemRequest(x.Id, x.Qty)).ToList(),
            "Penerimaan penuh dari endpoint kompatibilitas."), ct);
    }

    public async Task<StockTransferDto> RejectAsync(Guid userId, Guid transferId, CancellationToken ct = default)
    {
        var transfer = await GetAggregateAsync(transferId, ct);

        if (transfer == null)
        {
            throw new InvalidOperationException("Transfer Stok tidak ditemukan.");
        }

        await EnsureEnabledAsync(transfer.FromStockLocationId, ct);
        await EnsureEnabledAsync(transfer.ToStockLocationId, ct);

        if (transfer.Status is not (StockTransferStatus.InTransit or StockTransferStatus.Pending))
        {
            throw new InvalidOperationException("Transfer Stok sudah diproses sebelumnya.");
        }

        await EnsureTransferApprovalAccessAsync(transfer.ToStockLocationId, ct);

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
                        item.ProductId, transfer.FromStockLocationId, item.Qty,
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
                        item.ProductId, transfer.FromStockLocationId, 1,
                        StockMovementType.TransferIn, "stock_transfer", transfer.Id,
                        $"Pengembalian stok akibat transfer ditolak {transfer.TransferNumber}", ct,
                        item.ProductVariantId, productSerialId: serial.Id, documentItemId: item.Id);
                }
                else
                {
                    await _stockService.AddMovementAsync(
                        item.ProductId, transfer.FromStockLocationId, item.Qty,
                        StockMovementType.TransferIn, "stock_transfer", transfer.Id,
                        $"Pengembalian stok akibat transfer ditolak {transfer.TransferNumber}", ct,
                        item.ProductVariantId, documentItemId: item.Id);
                }
            }

            transfer.Status = StockTransferStatus.Returned;
            transfer.ApprovedBy = userId;
            transfer.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            await _stockEvents.EnqueueCurrentStockAsync(
                transfer.FromStockLocationId,
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
        StockTransfer transfer, Product product, StockTransferItemRequest itemReq, decimal unitCost, CancellationToken ct)
    {
        if (itemReq.Qty <= 0)
        {
            throw new InvalidOperationException($"Qty transfer untuk {product.Name} harus lebih dari 0.");
        }

        var stock = await _dbContext.InventoryStocks
            .FirstOrDefaultAsync(s => s.ProductId == itemReq.ProductId &&
                s.ProductVariantId == itemReq.ProductVariantId &&
                s.StockLocationId == transfer.FromStockLocationId, ct);

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
            Qty = itemReq.Qty,
            UnitCostSnapshot = unitCost
        };
        _dbContext.StockTransferItems.Add(item);

        await _stockService.AddMovementAsync(
            itemReq.ProductId, transfer.FromStockLocationId, -itemReq.Qty,
            StockMovementType.TransferOut, "stock_transfer", transfer.Id,
            $"Transfer keluar ke cabang via {transfer.TransferNumber}", ct,
            itemReq.ProductVariantId, documentItemId: item.Id);
    }

    private async Task AddBatchTransferItemAsync(
        StockTransfer transfer, Product product, StockTransferItemRequest itemReq, decimal unitCost, CancellationToken ct)
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
        if (_dbContext.Database.IsRelational())
        {
            await _dbContext.Database.SqlQuery<int>(
                $"SELECT 1 AS \"Value\" FROM \"ProductBatches\" WHERE \"Id\" = {itemReq.ProductBatchId.Value} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
        }

        var batch = await _dbContext.ProductBatches.FirstOrDefaultAsync(x =>
            x.Id == itemReq.ProductBatchId.Value &&
            x.ProductId == itemReq.ProductId &&
            x.ProductVariantId == itemReq.ProductVariantId &&
            x.StockLocationId == transfer.FromStockLocationId, ct)
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
            Qty = itemReq.Qty,
            UnitCostSnapshot = unitCost
        };
        _dbContext.StockTransferItems.Add(item);

        await _stockService.AddMovementAsync(
            itemReq.ProductId, transfer.FromStockLocationId, -itemReq.Qty,
            StockMovementType.TransferOut, "stock_transfer", transfer.Id,
            $"Transfer keluar ke cabang via {transfer.TransferNumber}", ct,
            itemReq.ProductVariantId, batch.Id, documentItemId: item.Id);
    }

    private async Task AddSerialTransferItemsAsync(
        StockTransfer transfer, Product product, StockTransferItemRequest itemReq,
        HashSet<Guid> usedSerialIds, decimal unitCost, CancellationToken ct)
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

        if (_dbContext.Database.IsRelational())
        {
            foreach (var serialId in serialIds)
            {
                await _dbContext.Database.SqlQuery<int>(
                    $"SELECT 1 AS \"Value\" FROM product_serials WHERE \"Id\" = {serialId} FOR UPDATE")
                    .SingleOrDefaultAsync(ct);
            }
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
                serial.StockLocationId != transfer.FromStockLocationId ||
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
                Qty = 1,
                UnitCostSnapshot = unitCost
            };
            _dbContext.StockTransferItems.Add(item);

            await _stockService.AddMovementAsync(
                itemReq.ProductId, transfer.FromStockLocationId, -1,
                StockMovementType.TransferOut, "stock_transfer", transfer.Id,
                $"Transfer keluar ke cabang via {transfer.TransferNumber}", ct,
                itemReq.ProductVariantId, productSerialId: serial.Id, documentItemId: item.Id);
        }
    }

    private async Task ReceiveBatchItemAsync(
        StockTransfer transfer, StockTransferItem item, decimal receivedQty, string? reason, CancellationToken ct)
    {
        if (receivedQty == 0) return;
        var sourceBatch = item.ProductBatch
            ?? await _dbContext.ProductBatches.FirstOrDefaultAsync(x => x.Id == item.ProductBatchId, ct)
            ?? throw new InvalidOperationException("Batch transfer tidak ditemukan.");

        var destinationBatch = await _dbContext.ProductBatches.FirstOrDefaultAsync(x =>
            x.StockLocationId == transfer.ToStockLocationId &&
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
                StockLocationId = transfer.ToStockLocationId,
                BatchNumber = sourceBatch.BatchNumber,
                ExpiryDate = sourceBatch.ExpiryDate,
                QtyProduction = receivedQty,
                QtyRemaining = receivedQty,
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
            destinationBatch.QtyProduction += receivedQty;
            destinationBatch.QtyRemaining += receivedQty;
            destinationBatch.UpdatedAt = DateTime.UtcNow;
        }

        await AddReceivedMovementAsync(transfer, item, receivedQty, destinationBatch.Id, null, reason, ct);
    }

    private async Task ReceiveSerialItemAsync(
        StockTransfer transfer, StockTransferItem item, decimal receivedQty, string? reason, CancellationToken ct)
    {
        if (receivedQty is not (0 or 1))
            throw new StockIntegrityException(StockIntegrityErrorCodes.TransferSerialReceiptInvalid,
                "Qty penerimaan setiap serial harus nol atau satu.");
        var serial = item.ProductSerial
            ?? await _dbContext.ProductSerials.FirstOrDefaultAsync(x => x.Id == item.ProductSerialId, ct)
            ?? throw new InvalidOperationException("Serial transfer tidak ditemukan.");

        if (serial.Status != ProductSerialStatuses.InTransfer)
        {
            throw new InvalidOperationException($"Serial {serial.SerialNumber} tidak sedang dalam transfer.");
        }

        if (receivedQty == 1)
        {
            serial.StockLocationId = transfer.ToStockLocationId;
            serial.Status = ProductSerialStatuses.Available;
        }
        else
        {
            serial.Status = ProductSerialStatuses.WrittenOff;
        }
        serial.UpdatedAt = DateTime.UtcNow;

        if (receivedQty == 1)
            await AddReceivedMovementAsync(transfer, item, 1, null, serial.Id, reason, ct);
    }

    private Task AddReceivedMovementAsync(
        StockTransfer transfer,
        StockTransferItem item,
        decimal receivedQty,
        Guid? productBatchId,
        Guid? productSerialId,
        string? varianceReason,
        CancellationToken ct)
        => _stockService.AddMovementAsync(
            item.ProductId, transfer.ToStockLocationId, receivedQty,
            StockMovementType.TransferIn, "stock_transfer", transfer.Id,
            varianceReason is null
                ? $"Penerimaan transfer {transfer.TransferNumber}"
                : $"Penerimaan transfer {transfer.TransferNumber} dengan selisih ({varianceReason})",
            ct, item.ProductVariantId, productBatchId, productSerialId, item.Id);

    private static readonly IReadOnlySet<string> NegativeVarianceReasons = new HashSet<string>(StringComparer.Ordinal)
    {
        "short_shipped", "lost_in_transit", "damaged", "counting_error", "other"
    };
    private static readonly IReadOnlySet<string> PositiveVarianceReasons = new HashSet<string>(StringComparer.Ordinal)
    {
        "over_shipped", "counting_error", "other"
    };

    private static void ValidateVariance(decimal difference, string? reason, string? note)
    {
        if (difference == 0)
        {
            if (reason is not null || note is not null)
                throw new StockIntegrityException(StockIntegrityErrorCodes.TransferVarianceReasonInvalid,
                    "Alasan selisih tidak boleh diisi jika jumlah diterima sesuai.");
            return;
        }
        if (reason is null || note is null)
            throw new StockIntegrityException(StockIntegrityErrorCodes.TransferVarianceReasonRequired,
                "Alasan dan catatan wajib diisi untuk penerimaan yang memiliki selisih.");
        var valid = difference < 0 ? NegativeVarianceReasons.Contains(reason) : PositiveVarianceReasons.Contains(reason);
        if (!valid)
            throw new StockIntegrityException(StockIntegrityErrorCodes.TransferVarianceReasonInvalid,
                "Alasan selisih tidak sesuai dengan jumlah kurang atau lebih.");
    }

    private IQueryable<StockTransfer> TransferQuery(bool tracking = false)
    {
        var query = _dbContext.StockTransfers
            .Include(t => t.FromStockLocation)
            .Include(t => t.ToStockLocation)
            .Include(t => t.RequestedByUser)
            .Include(t => t.ApprovedByUser)
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .Include(t => t.Items).ThenInclude(i => i.ProductVariant)
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
            t.FromStockLocationId,
            t.FromStockLocation?.Name ?? string.Empty,
            t.FromStockLocation?.Type ?? string.Empty,
            t.FromStockLocation?.OutletId,
            t.ToStockLocationId,
            t.ToStockLocation?.Name ?? string.Empty,
            t.ToStockLocation?.Type ?? string.Empty,
            t.ToStockLocation?.OutletId,
            t.TransferNumber,
            t.Status,
            t.RequestedBy,
            t.RequestedByUser?.Name ?? string.Empty,
            t.ApprovedBy,
            t.ApprovedByUser?.Name,
            t.CreatedAt,
            t.Items.Select(i => new StockTransferItemDto(
                i.Id,
                i.ProductId,
                i.Product?.Name ?? string.Empty,
                i.ProductVariant?.Sku ?? i.Product?.Sku ?? string.Empty,
                i.Qty
            )
            {
                ReceivedQty = i.ReceivedQty,
                DifferenceQty = i.DifferenceQty,
                VarianceReasonCode = i.VarianceReasonCode,
                VarianceNote = i.VarianceNote,
                UnitCostSnapshot = i.UnitCostSnapshot,
                ProductVariantId = i.ProductVariantId,
                ProductBatchId = i.ProductBatchId,
                BatchNumber = i.ProductBatch?.BatchNumber,
                ExpiryDate = i.ProductBatch?.ExpiryDate,
                ProductSerialId = i.ProductSerialId,
                SerialNumber = i.ProductSerial?.SerialNumber
            }).ToList()
        )
        {
            ReceivedAt = t.ReceivedAt,
            ReceiptNote = t.ReceiptNote
        };
    }

    private async Task EnsureTransferApprovalAccessAsync(Guid targetStockLocationId, CancellationToken ct)
    {
        if (!await HasLocationAccessAsync(targetStockLocationId, ct))
            throw new UnauthorizedAccessException("Approve atau reject transfer hanya bisa dilakukan oleh lokasi tujuan.");
    }

    private async Task EnsureLocationAccessAsync(Guid stockLocationId, CancellationToken ct)
    {
        if (!await HasLocationAccessAsync(stockLocationId, ct))
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke lokasi stok tersebut.");
    }

    private async Task<bool> HasLocationAccessAsync(Guid stockLocationId, CancellationToken ct)
    {
        if (_currentUserService.Role is "Owner" or "Admin") return true;

        var outletId = await ResolveLocationOutletAsync(stockLocationId, ct);
        if (outletId == _currentUserService.OutletId) return true;

        var userId = _currentUserService.UserId ?? Guid.Empty;
        return await _dbContext.UserStockLocationAccesses.AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.StockLocationId == stockLocationId, ct);
    }

    private async Task<Guid?> ResolveLocationOutletAsync(Guid stockLocationId, CancellationToken ct)
    {
        var location = await _dbContext.StockLocations.AsNoTracking()
            .Where(x => x.Id == stockLocationId)
            .Select(x => new { x.OutletId, x.IsActive })
            .SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Lokasi stok tidak ditemukan.");

        if (!location.IsActive)
        {
            throw new InvalidOperationException("Lokasi stok tidak aktif.");
        }

        return location.OutletId;
    }

    private async Task EnsureEnabledAsync(Guid stockLocationId, CancellationToken ct)
    {
        if (_capabilityEvaluator is null)
        {
            return;
        }

        var outletId = await ResolveLocationOutletAsync(stockLocationId, ct);
        await _capabilityEvaluator.EnsureEnabledAsync(CapabilityCodes.InventoryTransfer, outletId, ct);
    }
}
