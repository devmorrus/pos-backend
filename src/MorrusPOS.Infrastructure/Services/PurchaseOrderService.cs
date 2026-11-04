using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Application.Features.Suppliers;
using MorrusPOS.Application.Features.Capabilities;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPosNotificationService _notificationService;
    private readonly IAccountingIntegrationService? _accountingIntegrationService;
    private readonly ICapabilityEvaluator? _capabilities;

    public PurchaseOrderService(
        AppDbContext dbContext,
        IStockService stockService,
        ICurrentUserService currentUserService,
        IPosNotificationService notificationService,
        IAccountingIntegrationService? accountingIntegrationService = null,
        ICapabilityEvaluator? capabilities = null)
    {
        _dbContext = dbContext;
        _stockService = stockService;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _accountingIntegrationService = accountingIntegrationService;
        _capabilities = capabilities;
    }

    public async Task<PurchaseOrderDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var po = await _dbContext.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Outlet)
            .Include(p => p.CreatedByUser)
            .Include(p => p.Items).ThenInclude(i => i.Product)
            .Include(p => p.Items).ThenInclude(i => i.ProductVariant)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (po == null)
            throw new InvalidOperationException("Purchase Order tidak ditemukan.");

        await EnsureOutletAccessibleAsync(po.OutletId, ct);
        if (_capabilities is not null)
            await _capabilities.EnsureEnabledAsync(CapabilityCodes.ProcurementPurchasing, po.OutletId, ct);

        return MapToDto(po);
    }

    public async Task<IReadOnlyList<PurchaseOrderDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default)
    {
        await EnsureOutletAccessibleAsync(outletId, ct);
        if (_capabilities is not null)
            await _capabilities.EnsureEnabledAsync(CapabilityCodes.ProcurementPurchasing, outletId, ct);
        var pos = await _dbContext.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Outlet)
            .Include(p => p.CreatedByUser)
            .Include(p => p.Items).ThenInclude(i => i.Product)
            .Include(p => p.Items).ThenInclude(i => i.ProductVariant)
            .Where(p => p.OutletId == outletId)
            .OrderByDescending(p => p.PoDate)
            .ToListAsync(ct);

        return pos.Select(MapToDto).ToList();
    }

    public async Task<PurchaseOrderDto> CreateAsync(Guid userId, CreatePurchaseOrderRequest request, CancellationToken ct = default)
    {
        if (_capabilities is not null)
            await _capabilities.EnsureEnabledAsync(CapabilityCodes.ProcurementPurchasing, request.OutletId, ct);
        await EnsureOutletAccessibleAsync(request.OutletId, ct);

        // 1. Validate Supplier & Outlet
        var supplier = await _dbContext.Suppliers.FindAsync(new object[] { request.SupplierId }, ct);
        if (supplier == null || !supplier.IsActive)
            throw new InvalidOperationException("Supplier tidak ditemukan atau tidak aktif.");

        var outlet = await _dbContext.Outlets.FindAsync(new object[] { request.OutletId }, ct);
        if (outlet == null || !outlet.IsActive)
            throw new InvalidOperationException("Outlet tidak ditemukan atau tidak aktif.");

        if (request.Items.Count == 0)
            throw new InvalidOperationException("Purchase Order wajib memiliki minimal satu item.");

        // 2. Validate payment type
        if (request.PaymentType is not (PurchaseOrderPaymentType.Cash or PurchaseOrderPaymentType.Tempo or PurchaseOrderPaymentType.Consignment))
            throw new InvalidOperationException("PaymentType hanya boleh cash, tempo, atau consignment.");

        if (request.PaymentType == PurchaseOrderPaymentType.Tempo && request.DueDate == null)
            throw new InvalidOperationException("DueDate wajib diisi untuk pembayaran bertipe Tempo.");

        if (request.PaymentType == PurchaseOrderPaymentType.Consignment && request.DueDate != null)
            throw new InvalidOperationException("DueDate tidak boleh diisi untuk pembayaran bertipe Konsinyasi.");

        // 3. Generate PO number (non-blocking: random suffix to avoid sequence lock)
        var rand = new Random();
        var poNumber = $"PO-{DateTime.UtcNow:yyyyMMddHHmmss}-{rand.Next(1000, 9999)}";

        // 4. Build PO entity
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            SupplierId = request.SupplierId,
            OutletId = request.OutletId,
            PoNumber = poNumber,
            PoDate = DateTime.UtcNow,
            PaymentType = request.PaymentType,
            Status = PurchaseOrderStatus.Draft,
            DueDate = request.DueDate,
            TotalAmount = 0,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.PurchaseOrders.Add(po);

        // 5. Process items
        decimal total = 0;
        foreach (var itemReq in request.Items)
        {
            if (itemReq.Qty <= 0)
                throw new InvalidOperationException("Qty item PO harus lebih dari 0.");

            if (itemReq.UnitCost <= 0)
                throw new InvalidOperationException("Unit cost item PO harus lebih dari 0.");

            var product = await _dbContext.Products.FindAsync(new object[] { itemReq.ProductId }, ct);
            if (product == null || !product.IsActive)
                throw new InvalidOperationException($"Produk tidak valid atau tidak aktif.");

            var lineTotal = itemReq.Qty * itemReq.UnitCost;
            total += lineTotal;

            _dbContext.PurchaseOrderItems.Add(new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = po.Id,
                ProductId = itemReq.ProductId,
                ProductVariantId = itemReq.ProductVariantId,
                Qty = itemReq.Qty,
                QtyReceived = 0,
                UnitCost = itemReq.UnitCost,
                TotalCost = lineTotal
            });
        }

        po.TotalAmount = total;
        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(po.Id, ct);
    }

    public async Task<PurchaseOrderDto> UpdateStatusAsync(Guid userId, Guid poId, UpdatePoStatusRequest request, CancellationToken ct = default)
    {
        var po = await _dbContext.PurchaseOrders
            .Include(p => p.Items).ThenInclude(i => i.Product)
            .Include(p => p.Outlet)
            .FirstOrDefaultAsync(p => p.Id == poId, ct);

        if (po == null)
            throw new InvalidOperationException("Purchase Order tidak ditemukan.");

        if (_capabilities is not null)
            await _capabilities.EnsureEnabledAsync(CapabilityCodes.ProcurementPurchasing, po.OutletId, ct);

        await EnsureOutletAccessibleAsync(po.OutletId, ct);

        // Prevent backward state transitions
        if (po.Status == PurchaseOrderStatus.Completed || po.Status == PurchaseOrderStatus.Cancelled)
            throw new InvalidOperationException($"Purchase Order sudah berstatus '{po.Status}', tidak bisa diubah kembali.");

        // Only allow completing to "completed" or "cancelled"
        var allowedNextStatuses = new[] { PurchaseOrderStatus.Pending, PurchaseOrderStatus.Completed, PurchaseOrderStatus.Cancelled };
        if (!allowedNextStatuses.Contains(request.Status))
            throw new InvalidOperationException($"Status '{request.Status}' tidak valid.");

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            // === Handle Completion: Receive Goods ===
            if (request.Status == PurchaseOrderStatus.Completed)
            {
                // shortcut: direct PO completion cannot allocate batch/serial; use the receiving page.
                if (po.Items.Any(i => i.Product is not null && i.Product.TrackingMode != ProductTrackingModes.Quantity))
                {
                    throw new InvalidOperationException("PO dengan produk batch/serial harus diterima melalui halaman penerimaan barang.");
                }

                var isConsignmentPayment = po.PaymentType == PurchaseOrderPaymentType.Consignment;

                Consignment? consignment = null;
                if (isConsignmentPayment)
                {
                    consignment = new Consignment
                    {
                        Id = Guid.NewGuid(),
                        SupplierId = po.SupplierId,
                        OutletId = po.OutletId,
                        ConsignmentNumber = $"CSG-PO-{po.PoNumber}",
                        ReceiveDate = DateTime.UtcNow,
                        Status = ConsignmentStatus.Received,
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow,
                        Items = po.Items.Select(item => new ConsignmentItem
                        {
                            Id = Guid.NewGuid(),
                            ProductId = item.ProductId,
                            Qty = item.Qty,
                            UnitCost = item.UnitCost,
                            UnitPrice = item.Product != null ? item.Product.BasePrice : item.UnitCost
                        }).ToList()
                    };
                    _dbContext.Consignments.Add(consignment);
                }

                foreach (var item in po.Items)
                {
                    if (isConsignmentPayment)
                    {
                        // 1. Mark product as consignment product
                        if (item.Product != null)
                        {
                            item.Product.IsConsignment = true;
                            item.Product.UpdatedAt = DateTime.UtcNow;
                        }

                        // 2. Add stock movement (consignment_in)
                        await _stockService.AddMovementAsync(
                            productId: item.ProductId,
                            outletId: po.OutletId,
                            qtyChange: item.Qty,
                            movementType: "consignment_in",
                            referenceType: "consignment",
                            referenceId: consignment!.Id,
                            note: $"Penerimaan barang konsinyasi via PO {po.PoNumber}",
                            ct: ct
                        );
                    }
                    else
                    {
                        // 1. Add stock movement (purchase_in)
                        await _stockService.AddMovementAsync(
                            productId: item.ProductId,
                            outletId: po.OutletId,
                            qtyChange: item.Qty,
                            movementType: "purchase_in",
                            referenceType: "purchase_order",
                            referenceId: po.Id,
                            note: $"Penerimaan barang dari PO {po.PoNumber}",
                            ct: ct
                        );
                    }

                    // 2. Update HPP (CostPrice) and write audit log
                    if (item.Product != null && item.Product.CostPrice != item.UnitCost)
                    {
                        var oldCost = item.Product.CostPrice;
                        item.Product.CostPrice = item.UnitCost;
                        item.Product.UpdatedAt = DateTime.UtcNow;

                        _dbContext.AuditLogs.Add(new AuditLog
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            OutletId = po.OutletId,
                            EntityType = "product",
                            EntityId = item.ProductId,
                            Action = "cost_price_update",
                            OldValueJson = JsonSerializer.Serialize(new { CostPrice = oldCost }),
                            NewValueJson = JsonSerializer.Serialize(new { CostPrice = item.UnitCost, PoNumber = po.PoNumber }),
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // 3. If payment type is Tempo, automatically create SupplierDebt
                if (po.PaymentType == PurchaseOrderPaymentType.Tempo)
                {
                    // Only create debt if it doesn't already exist for this PO
                    var existingDebt = await _dbContext.SupplierDebts
                        .FirstOrDefaultAsync(d => d.PurchaseOrderId == po.Id, ct);

                    if (existingDebt == null)
                    {
                        var debt = new SupplierDebt
                        {
                            Id = Guid.NewGuid(),
                            SupplierId = po.SupplierId,
                            PurchaseOrderId = po.Id,
                            DueDate = po.DueDate!.Value,
                            Amount = po.TotalAmount,
                            PaidAmount = 0,
                            RemainingAmount = po.TotalAmount,
                            Status = SupplierDebtStatus.Unpaid,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _dbContext.SupplierDebts.Add(debt);
                    }
                }
            }

            po.Status = request.Status;
            po.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            if (request.Status == PurchaseOrderStatus.Completed && _accountingIntegrationService != null)
            {
                await _accountingIntegrationService.EnsurePurchaseOrderPostedAsync(po.Id, ct);
            }

            // After SaveChangesAsync, PostgreSQL trigger has already updated InventoryStock.
            // Now query the latest stock values and broadcast to connected POS clients via SignalR.
            if (request.Status == PurchaseOrderStatus.Completed)
            {
                var productIds = po.Items.Select(i => i.ProductId).ToList();
                var stocks = await _dbContext.InventoryStocks
                    .Where(s => s.OutletId == po.OutletId && productIds.Contains(s.ProductId))
                    .ToListAsync(ct);

                var stockUpdates = stocks
                    .Select(s => new StockUpdateItem(s.ProductId, s.QtyOnHand))
                    .ToList();

                if (stockUpdates.Any())
                {
                    await _notificationService.SendStockUpdateAsync(po.OutletId, stockUpdates, ct);
                }
            }

            await dbTx.CommitAsync(ct);

            return await GetByIdAsync(po.Id, ct);
        }
        catch (Exception)
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<PurchaseOrderDto> ReceiveGoodsAsync(Guid userId, Guid poId, ReceiveGoodsRequest request, CancellationToken ct = default)
    {
        if (_capabilities is not null && request.Items.Any(x =>
                !string.IsNullOrWhiteSpace(x.BatchNumber) || x.ExpiryDate.HasValue || x.Batches?.Count > 0))
            await _capabilities.EnsureEnabledAsync(CapabilityCodes.CatalogBatchExpiry, null, ct);
        if (_capabilities is not null && request.Items.Any(x => x.SerialNumbers?.Count > 0))
            await _capabilities.EnsureEnabledAsync(CapabilityCodes.CatalogSerialTracking, null, ct);
        var po = await _dbContext.PurchaseOrders
            .Include(p => p.Items).ThenInclude(i => i.Product)
            .Include(p => p.Items).ThenInclude(i => i.ProductVariant)
            .Include(p => p.Outlet)
            .FirstOrDefaultAsync(p => p.Id == poId, ct);

        if (po == null)
            throw new InvalidOperationException("Purchase Order tidak ditemukan.");

        if (_capabilities is not null)
            await _capabilities.EnsureEnabledAsync(CapabilityCodes.ProcurementPurchasing, po.OutletId, ct);

        await EnsureOutletAccessibleAsync(po.OutletId, ct);

        if (po.Status == PurchaseOrderStatus.Completed || po.Status == PurchaseOrderStatus.Cancelled)
            throw new InvalidOperationException($"Purchase Order sudah berstatus '{po.Status}', tidak bisa menerima barang lagi.");

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var receivingNote = new ReceivingNote
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = po.Id,
                ReceivingNumber = $"RCV-{po.PoNumber}-{DateTime.UtcNow:HHmmss}",
                ReceivedDate = DateTime.UtcNow,
                Notes = request.Notes,
                ReceivedBy = userId,
                ReceivedByUser = default!
            };
            
            _dbContext.Entry(receivingNote).Property("ReceivedByUserId").CurrentValue = userId;
            _dbContext.ReceivingNotes.Add(receivingNote);

            bool allCompleted = true;

            foreach (var itemReq in request.Items)
            {
                if (itemReq.QtyReceived <= 0) continue;

                var poItem = po.Items.FirstOrDefault(i => i.ProductId == itemReq.ProductId && i.ProductVariantId == itemReq.ProductVariantId);
                if (poItem == null)
                    throw new InvalidOperationException("Item yang diterima tidak cocok dengan item di Purchase Order.");

                poItem.QtyReceived += itemReq.QtyReceived;

                var receivingItem = new ReceivingNoteItem
                {
                    Id = Guid.NewGuid(),
                    ReceivingNoteId = receivingNote.Id,
                    ProductId = itemReq.ProductId,
                    ProductVariantId = itemReq.ProductVariantId,
                    QtyReceived = itemReq.QtyReceived,
                    BatchNumber = itemReq.BatchNumber,
                    ExpiryDate = itemReq.ExpiryDate
                };
                _dbContext.ReceivingNoteItems.Add(receivingItem);

                await ReceiveTrackedStockAsync(
                    poItem.Product, po.OutletId, itemReq, receivingNote.Id, receivingItem.Id,
                    $"Penerimaan barang dari PO {po.PoNumber} (No. RCV: {receivingNote.ReceivingNumber})", ct);
            }

            foreach (var item in po.Items)
            {
                if (item.QtyReceived < item.Qty)
                {
                    allCompleted = false;
                    break;
                }
            }

            po.Status = allCompleted ? PurchaseOrderStatus.Completed : PurchaseOrderStatus.PartiallyReceived;
            po.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);

            if (po.Status == PurchaseOrderStatus.Completed && _accountingIntegrationService != null)
            {
                await _accountingIntegrationService.EnsurePurchaseOrderPostedAsync(po.Id, ct);
            }

            var productIds = request.Items.Select(i => i.ProductId).ToList();
            var stocks = await _dbContext.InventoryStocks
                .Where(s => s.OutletId == po.OutletId && productIds.Contains(s.ProductId))
                .ToListAsync(ct);

            var stockUpdates = stocks
                .Select(s => new StockUpdateItem(s.ProductId, s.QtyOnHand))
                .ToList();

            if (stockUpdates.Any())
            {
                await _notificationService.SendStockUpdateAsync(po.OutletId, stockUpdates, ct);
            }

            await dbTx.CommitAsync(ct);

            return await GetByIdAsync(po.Id, ct);
        }
        catch (Exception)
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task ReceiveTrackedStockAsync(
        Product product,
        Guid outletId,
        ReceivingItemRequest request,
        Guid receivingNoteId,
        Guid receivingItemId,
        string note,
        CancellationToken ct)
    {
        if (product.TrackingMode == ProductTrackingModes.None) return;

        if (product.TrackingMode == ProductTrackingModes.Quantity)
        {
            await _stockService.AddMovementAsync(product.Id, outletId, request.QtyReceived,
                StockMovementType.PurchaseIn, "receiving_note", receivingNoteId, note, ct,
                request.ProductVariantId, documentItemId: receivingItemId);
            return;
        }

        if (product.TrackingMode == ProductTrackingModes.Batch)
        {
            var allocations = request.Batches?.ToList() ?? [];
            if (allocations.Count == 0 && !string.IsNullOrWhiteSpace(request.BatchNumber))
                allocations.Add(new ReceivingBatchRequest(request.BatchNumber, request.ExpiryDate, request.QtyReceived));
            if (allocations.Count == 0 || allocations.Any(x => x.Qty <= 0 || string.IsNullOrWhiteSpace(x.BatchNumber)) ||
                allocations.Sum(x => x.Qty) != request.QtyReceived)
                throw new InvalidOperationException("Alokasi batch wajib diisi dan totalnya harus sama dengan qty penerimaan.");

            foreach (var allocation in allocations)
            {
                var number = allocation.BatchNumber.Trim();
                var batch = await _dbContext.ProductBatches.FirstOrDefaultAsync(x =>
                    x.ProductId == product.Id && x.ProductVariantId == request.ProductVariantId &&
                    x.OutletId == outletId && x.BatchNumber == number, ct);
                if (batch is null)
                {
                    batch = new ProductBatch
                    {
                        Id = Guid.NewGuid(), ProductId = product.Id, ProductVariantId = request.ProductVariantId,
                        OutletId = outletId, BatchNumber = number, ExpiryDate = allocation.ExpiryDate,
                        QtyProduction = allocation.Qty, QtyRemaining = allocation.Qty,
                        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                    };
                    _dbContext.ProductBatches.Add(batch);
                }
                else
                {
                    if (batch.ExpiryDate?.Date != allocation.ExpiryDate?.Date)
                        throw new InvalidOperationException($"Expiry batch {number} berbeda dari penerimaan sebelumnya.");
                    batch.QtyProduction += allocation.Qty;
                    batch.QtyRemaining += allocation.Qty;
                    batch.UpdatedAt = DateTime.UtcNow;
                }
                await _stockService.AddMovementAsync(product.Id, outletId, allocation.Qty,
                    StockMovementType.PurchaseIn, "receiving_note", receivingNoteId, note, ct,
                    request.ProductVariantId, batch.Id, documentItemId: receivingItemId);
            }
            return;
        }

        var serialNumbers = request.SerialNumbers?.Select(x => x.Trim()).Where(x => x.Length > 0).ToList() ?? [];
        if (request.QtyReceived != decimal.Truncate(request.QtyReceived) || serialNumbers.Count != request.QtyReceived ||
            serialNumbers.Select(x => x.ToUpperInvariant()).Distinct().Count() != serialNumbers.Count)
            throw new InvalidOperationException("Jumlah serial unik harus sama dengan qty penerimaan dan qty harus bilangan bulat.");

        foreach (var number in serialNumbers)
        {
            var normalized = number.ToUpperInvariant();
            if (await _dbContext.ProductSerials.AnyAsync(x => x.NormalizedSerialNumber == normalized, ct))
                throw new InvalidOperationException($"Serial {number} sudah terdaftar.");
            var serial = new ProductSerial
            {
                Id = Guid.NewGuid(), ProductId = product.Id, ProductVariantId = request.ProductVariantId,
                OutletId = outletId, ReceivingNoteItemId = receivingItemId,
                SerialNumber = number, NormalizedSerialNumber = normalized,
                Status = ProductSerialStatuses.Available, ReceivedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            _dbContext.ProductSerials.Add(serial);
            await _stockService.AddMovementAsync(product.Id, outletId, 1,
                StockMovementType.PurchaseIn, "receiving_note", receivingNoteId, note, ct,
                request.ProductVariantId, productSerialId: serial.Id, documentItemId: receivingItemId);
        }
    }

    private static PurchaseOrderDto MapToDto(PurchaseOrder po) => new(
        po.Id,
        po.SupplierId,
        po.Supplier?.Name ?? string.Empty,
        po.OutletId,
        po.Outlet?.Name ?? string.Empty,
        po.PoNumber,
        po.PoDate,
        po.PaymentType,
        po.Status,
        po.DueDate,
        po.TotalAmount,
        po.Items.Select(i => new PurchaseOrderItemDto(
            i.ProductId,
            i.Product?.Name ?? string.Empty,
            i.ProductVariant?.Sku ?? i.Product?.Sku ?? string.Empty,
            i.Qty,
            i.UnitCost,
            i.TotalCost,
            i.Product?.BasePrice ?? 0
        )
        {
            ProductVariantId = i.ProductVariantId,
            QtyReceived = i.QtyReceived,
            TrackingMode = i.Product?.TrackingMode ?? ProductTrackingModes.Quantity
        }).ToList()
    );

    private async Task EnsureOutletAccessibleAsync(Guid outletId, CancellationToken ct)
    {
        var outlet = await _dbContext.Outlets
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == outletId, ct);

        if (outlet == null || !outlet.IsActive)
        {
            throw new InvalidOperationException("Outlet tidak valid atau tidak aktif.");
        }

        if (_currentUserService.Role != "Owner" && _currentUserService.OutletId != outletId)
        {
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke outlet tersebut.");
        }
    }
}
