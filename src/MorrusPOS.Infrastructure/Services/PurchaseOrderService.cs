using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Suppliers;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockService;

    public PurchaseOrderService(AppDbContext dbContext, IStockService stockService)
    {
        _dbContext = dbContext;
        _stockService = stockService;
    }

    public async Task<PurchaseOrderDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var po = await _dbContext.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Outlet)
            .Include(p => p.CreatedByUser)
            .Include(p => p.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (po == null)
            throw new InvalidOperationException("Purchase Order tidak ditemukan.");

        return MapToDto(po);
    }

    public async Task<IReadOnlyList<PurchaseOrderDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default)
    {
        var pos = await _dbContext.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Outlet)
            .Include(p => p.CreatedByUser)
            .Include(p => p.Items).ThenInclude(i => i.Product)
            .Where(p => p.OutletId == outletId)
            .OrderByDescending(p => p.PoDate)
            .ToListAsync(ct);

        return pos.Select(MapToDto).ToList();
    }

    public async Task<PurchaseOrderDto> CreateAsync(Guid userId, CreatePurchaseOrderRequest request, CancellationToken ct = default)
    {
        // 1. Validate Supplier & Outlet
        var supplier = await _dbContext.Suppliers.FindAsync(new object[] { request.SupplierId }, ct);
        if (supplier == null || !supplier.IsActive)
            throw new InvalidOperationException("Supplier tidak ditemukan atau tidak aktif.");

        var outlet = await _dbContext.Outlets.FindAsync(new object[] { request.OutletId }, ct);
        if (outlet == null || !outlet.IsActive)
            throw new InvalidOperationException("Outlet tidak ditemukan atau tidak aktif.");

        // 2. Validate payment type
        if (request.PaymentType == PurchaseOrderPaymentType.Tempo && request.DueDate == null)
            throw new InvalidOperationException("DueDate wajib diisi untuk pembayaran bertipe Tempo.");

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
                Qty = itemReq.Qty,
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
                foreach (var item in po.Items)
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
            await dbTx.CommitAsync(ct);

            return await GetByIdAsync(po.Id, ct);
        }
        catch (Exception)
        {
            await dbTx.RollbackAsync(ct);
            throw;
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
            i.Product?.Sku ?? string.Empty,
            i.Qty,
            i.UnitCost,
            i.TotalCost
        )).ToList()
    );
}
