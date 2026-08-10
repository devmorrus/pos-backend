using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Suppliers;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class SupplierReturnService : ISupplierReturnService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStockService _stockService;
    private readonly IPosNotificationService _notificationService;

    public SupplierReturnService(
        AppDbContext dbContext,
        ICurrentUserService currentUserService,
        IStockService stockService,
        IPosNotificationService notificationService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _stockService = stockService;
        _notificationService = notificationService;
    }

    public async Task<IReadOnlyList<SupplierReturnListItemDto>> GetAsync(SupplierReturnFilters filters, CancellationToken ct = default)
    {
        var query = _dbContext.SupplierReturns
            .Include(sr => sr.Supplier)
            .Include(sr => sr.PurchaseOrder).ThenInclude(po => po.Outlet)
            .Include(sr => sr.CreatedByUser)
            .AsNoTracking()
            .AsQueryable();

        if (filters.OutletId.HasValue)
        {
            await EnsureOutletAccessibleAsync(filters.OutletId.Value, ct);
            query = query.Where(sr => sr.PurchaseOrder.OutletId == filters.OutletId.Value);
        }

        if (filters.SupplierId.HasValue)
        {
            query = query.Where(sr => sr.SupplierId == filters.SupplierId.Value);
        }

        if (filters.PurchaseOrderId.HasValue)
        {
            query = query.Where(sr => sr.PurchaseOrderId == filters.PurchaseOrderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.Status))
        {
            var status = filters.Status.Trim().ToLowerInvariant();
            query = query.Where(sr => sr.Status == status);
        }

        if (filters.DateFrom.HasValue)
        {
            var fromDate = filters.DateFrom.Value.Date;
            query = query.Where(sr => sr.ReturnDate >= fromDate);
        }

        if (filters.DateTo.HasValue)
        {
            var toDate = filters.DateTo.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(sr => sr.ReturnDate <= toDate);
        }

        var result = await query
            .OrderByDescending(sr => sr.ReturnDate)
            .Take(Math.Clamp(filters.Take, 1, 100))
            .ToListAsync(ct);

        return result.Select(MapToListDto).ToList();
    }

    public async Task<SupplierReturnDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var supplierReturn = await GetAggregateAsync(id, ct);
        if (supplierReturn == null)
        {
            throw new InvalidOperationException("Retur supplier tidak ditemukan.");
        }

        await EnsureOutletAccessibleAsync(supplierReturn.PurchaseOrder.OutletId, ct);
        return await MapToDtoAsync(supplierReturn, ct);
    }

    public async Task<IReadOnlyList<SupplierReturnPurchaseOrderLookupDto>> GetEligiblePurchaseOrdersAsync(Guid outletId, Guid? supplierId, CancellationToken ct = default)
    {
        await EnsureOutletAccessibleAsync(outletId, ct);

        var query = _dbContext.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Outlet)
            .Where(po => po.OutletId == outletId && po.Status == PurchaseOrderStatus.Completed)
            .AsNoTracking()
            .AsQueryable();

        if (supplierId.HasValue)
        {
            query = query.Where(po => po.SupplierId == supplierId.Value);
        }

        var purchaseOrders = await query
            .OrderByDescending(po => po.PoDate)
            .ToListAsync(ct);

        var eligible = new List<SupplierReturnPurchaseOrderLookupDto>();
        foreach (var purchaseOrder in purchaseOrders)
        {
            var items = await GetEligibleItemsAsync(purchaseOrder.Id, ct);
            if (items.Any(item => item.EligibleQty > 0))
            {
                eligible.Add(new SupplierReturnPurchaseOrderLookupDto(
                    purchaseOrder.Id,
                    purchaseOrder.PoNumber,
                    purchaseOrder.PoDate,
                    purchaseOrder.SupplierId,
                    purchaseOrder.Supplier.Name,
                    purchaseOrder.OutletId,
                    purchaseOrder.Outlet.Name,
                    purchaseOrder.TotalAmount
                ));
            }
        }

        return eligible;
    }

    public async Task<IReadOnlyList<SupplierReturnItemDto>> GetEligibleItemsAsync(Guid purchaseOrderId, CancellationToken ct = default)
    {
        var purchaseOrder = await _dbContext.PurchaseOrders
            .Include(po => po.Items).ThenInclude(item => item.Product)
            .Include(po => po.Outlet)
            .FirstOrDefaultAsync(po => po.Id == purchaseOrderId, ct);

        if (purchaseOrder == null)
        {
            throw new InvalidOperationException("Purchase order tidak ditemukan.");
        }

        await EnsureOutletAccessibleAsync(purchaseOrder.OutletId, ct);

        var previouslyReturned = await GetReturnedQtyByProductAsync(purchaseOrderId, excludingReturnId: null, ct);

        return purchaseOrder.Items
            .Select(item =>
            {
                var returnedQty = previouslyReturned.GetValueOrDefault(item.ProductId);
                var eligibleQty = Math.Max(0, item.Qty - returnedQty);
                return new SupplierReturnItemDto(
                    item.ProductId,
                    item.Product?.Name ?? string.Empty,
                    item.Product?.Sku ?? string.Empty,
                    0,
                    item.UnitCost,
                    0,
                    eligibleQty
                );
            })
            .Where(item => item.EligibleQty > 0)
            .ToList();
    }

    public async Task<SupplierReturnDto> CreateAsync(Guid userId, CreateSupplierReturnRequest request, CancellationToken ct = default)
    {
        var purchaseOrder = await ValidatePurchaseOrderAsync(request.SupplierId, request.PurchaseOrderId, ct);
        await EnsureOutletAccessibleAsync(purchaseOrder.OutletId, ct);

        var supplierReturn = new SupplierReturn
        {
            Id = Guid.NewGuid(),
            SupplierId = request.SupplierId,
            PurchaseOrderId = request.PurchaseOrderId,
            ReturnNumber = $"SR-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            ReturnDate = DateTime.SpecifyKind(request.ReturnDate, DateTimeKind.Utc),
            Status = SupplierReturnStatus.Draft,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await PopulateDraftItemsAsync(supplierReturn, purchaseOrder, request.Items, excludingReturnId: null, ct);

        _dbContext.SupplierReturns.Add(supplierReturn);
        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(supplierReturn.Id, ct);
    }

    public async Task<SupplierReturnDto> UpdateAsync(Guid userId, Guid id, UpdateSupplierReturnRequest request, CancellationToken ct = default)
    {
        var supplierReturn = await _dbContext.SupplierReturns
            .Include(sr => sr.PurchaseOrder).ThenInclude(po => po.Items).ThenInclude(item => item.Product)
            .Include(sr => sr.Items)
            .FirstOrDefaultAsync(sr => sr.Id == id, ct);

        if (supplierReturn == null)
        {
            throw new InvalidOperationException("Retur supplier tidak ditemukan.");
        }

        await EnsureOutletAccessibleAsync(supplierReturn.PurchaseOrder.OutletId, ct);

        if (supplierReturn.Status != SupplierReturnStatus.Draft)
        {
            throw new InvalidOperationException("Hanya retur supplier draft yang dapat diubah.");
        }

        supplierReturn.ReturnDate = DateTime.SpecifyKind(request.ReturnDate, DateTimeKind.Utc);
        supplierReturn.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        supplierReturn.UpdatedAt = DateTime.UtcNow;

        _dbContext.SupplierReturnItems.RemoveRange(supplierReturn.Items);
        supplierReturn.Items.Clear();

        await PopulateDraftItemsAsync(supplierReturn, supplierReturn.PurchaseOrder, request.Items, id, ct);
        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<SupplierReturnDto> UpdateStatusAsync(Guid userId, Guid id, UpdateSupplierReturnStatusRequest request, CancellationToken ct = default)
    {
        var supplierReturn = await _dbContext.SupplierReturns
            .Include(sr => sr.PurchaseOrder).ThenInclude(po => po.Outlet)
            .Include(sr => sr.Items)
            .FirstOrDefaultAsync(sr => sr.Id == id, ct);

        if (supplierReturn == null)
        {
            throw new InvalidOperationException("Retur supplier tidak ditemukan.");
        }

        await EnsureOutletAccessibleAsync(supplierReturn.PurchaseOrder.OutletId, ct);

        var targetStatus = request.Status.Trim().ToLowerInvariant();
        if (targetStatus == SupplierReturnStatus.Sent)
        {
            if (supplierReturn.Status != SupplierReturnStatus.Draft)
            {
                throw new InvalidOperationException("Hanya retur supplier draft yang dapat dikirim.");
            }

            using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                foreach (var item in supplierReturn.Items)
                {
                    await _stockService.AddMovementAsync(
                        item.ProductId,
                        supplierReturn.PurchaseOrder.OutletId,
                        -item.Qty,
                        StockMovementType.ConsignmentReturn,
                        "supplier_return",
                        supplierReturn.Id,
                        $"Retur supplier {supplierReturn.ReturnNumber}",
                        ct);
                }

                var supplierDebt = await _dbContext.SupplierDebts
                    .FirstOrDefaultAsync(debt => debt.PurchaseOrderId == supplierReturn.PurchaseOrderId, ct);

                if (supplierDebt != null && supplierDebt.RemainingAmount > 0)
                {
                    var adjustment = Math.Min(supplierDebt.RemainingAmount, supplierReturn.TotalAmount);
                    supplierDebt.Amount = Math.Max(0, supplierDebt.Amount - adjustment);
                    supplierDebt.RemainingAmount = Math.Max(0, supplierDebt.RemainingAmount - adjustment);
                    supplierDebt.Status = supplierDebt.RemainingAmount == 0
                        ? SupplierDebtStatus.Paid
                        : supplierDebt.PaidAmount > 0
                            ? SupplierDebtStatus.PartiallyPaid
                            : SupplierDebtStatus.Unpaid;
                    supplierDebt.UpdatedAt = DateTime.UtcNow;
                }

                supplierReturn.Status = SupplierReturnStatus.Sent;
                supplierReturn.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync(ct);
                await dbTx.CommitAsync(ct);
            }
            catch
            {
                await dbTx.RollbackAsync(ct);
                throw;
            }

            var stockUpdates = await _dbContext.InventoryStocks
                .Where(stock => stock.OutletId == supplierReturn.PurchaseOrder.OutletId
                    && supplierReturn.Items.Select(item => item.ProductId).Contains(stock.ProductId))
                .Select(stock => new StockUpdateItem(stock.ProductId, stock.QtyOnHand))
                .ToListAsync(ct);

            if (stockUpdates.Count > 0)
            {
                await _notificationService.SendStockUpdateAsync(supplierReturn.PurchaseOrder.OutletId, stockUpdates, ct);
            }
        }
        else if (targetStatus == SupplierReturnStatus.Completed)
        {
            if (supplierReturn.Status != SupplierReturnStatus.Sent)
            {
                throw new InvalidOperationException("Hanya retur supplier berstatus sent yang dapat diselesaikan.");
            }

            supplierReturn.Status = SupplierReturnStatus.Completed;
            supplierReturn.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
        }

        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var supplierReturn = await _dbContext.SupplierReturns
            .Include(sr => sr.PurchaseOrder)
            .Include(sr => sr.Items)
            .FirstOrDefaultAsync(sr => sr.Id == id, ct);

        if (supplierReturn == null)
        {
            throw new InvalidOperationException("Retur supplier tidak ditemukan.");
        }

        await EnsureOutletAccessibleAsync(supplierReturn.PurchaseOrder.OutletId, ct);

        if (supplierReturn.Status != SupplierReturnStatus.Draft)
        {
            throw new InvalidOperationException("Hanya retur supplier draft yang dapat dihapus.");
        }

        _dbContext.SupplierReturnItems.RemoveRange(supplierReturn.Items);
        _dbContext.SupplierReturns.Remove(supplierReturn);
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task PopulateDraftItemsAsync(
        SupplierReturn supplierReturn,
        PurchaseOrder purchaseOrder,
        IReadOnlyCollection<SupplierReturnItemRequest> items,
        Guid? excludingReturnId,
        CancellationToken ct)
    {
        var purchaseOrderItems = purchaseOrder.Items.ToDictionary(item => item.ProductId);
        var previouslyReturned = await GetReturnedQtyByProductAsync(purchaseOrder.Id, excludingReturnId, ct);
        decimal totalAmount = 0;

        foreach (var itemRequest in items)
        {
            if (!purchaseOrderItems.TryGetValue(itemRequest.ProductId, out var purchaseOrderItem))
            {
                throw new InvalidOperationException("Produk retur tidak ditemukan pada purchase order.");
            }

            var returnedQty = previouslyReturned.GetValueOrDefault(itemRequest.ProductId);
            var eligibleQty = purchaseOrderItem.Qty - returnedQty;
            if (itemRequest.Qty > eligibleQty)
            {
                throw new InvalidOperationException($"Qty retur untuk produk {purchaseOrderItem.Product?.Name ?? purchaseOrderItem.ProductId.ToString()} melebihi qty yang masih eligible.");
            }

            var lineTotal = itemRequest.Qty * purchaseOrderItem.UnitCost;
            supplierReturn.Items.Add(new SupplierReturnItem
            {
                Id = Guid.NewGuid(),
                SupplierReturnId = supplierReturn.Id,
                ProductId = itemRequest.ProductId,
                Qty = itemRequest.Qty,
                UnitCost = purchaseOrderItem.UnitCost,
                TotalCost = lineTotal
            });

            totalAmount += lineTotal;
        }

        supplierReturn.TotalAmount = totalAmount;
    }

    private async Task<PurchaseOrder> ValidatePurchaseOrderAsync(Guid supplierId, Guid purchaseOrderId, CancellationToken ct)
    {
        var purchaseOrder = await _dbContext.PurchaseOrders
            .Include(po => po.Items).ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(po => po.Id == purchaseOrderId, ct);

        if (purchaseOrder == null)
        {
            throw new InvalidOperationException("Purchase order tidak ditemukan.");
        }

        if (purchaseOrder.SupplierId != supplierId)
        {
            throw new InvalidOperationException("Supplier retur harus sesuai dengan supplier purchase order.");
        }

        if (purchaseOrder.Status != PurchaseOrderStatus.Completed && purchaseOrder.Status != PurchaseOrderStatus.Pending)
        {
            throw new InvalidOperationException("Hanya purchase order aktif atau completed yang boleh diretur.");
        }

        return purchaseOrder;
    }

    private async Task<Dictionary<Guid, decimal>> GetReturnedQtyByProductAsync(Guid purchaseOrderId, Guid? excludingReturnId, CancellationToken ct)
    {
        var query = _dbContext.SupplierReturnItems
            .Where(item => item.SupplierReturn.PurchaseOrderId == purchaseOrderId)
            .AsQueryable();

        if (excludingReturnId.HasValue)
        {
            query = query.Where(item => item.SupplierReturnId != excludingReturnId.Value);
        }

        var items = await query
            .GroupBy(item => item.ProductId)
            .Select(group => new { group.Key, Qty = group.Sum(item => item.Qty) })
            .ToListAsync(ct);

        return items.ToDictionary(item => item.Key, item => item.Qty);
    }

    private async Task<SupplierReturn?> GetAggregateAsync(Guid id, CancellationToken ct)
    {
        return await _dbContext.SupplierReturns
            .Include(sr => sr.Supplier)
            .Include(sr => sr.PurchaseOrder).ThenInclude(po => po.Outlet)
            .Include(sr => sr.CreatedByUser)
            .Include(sr => sr.Items).ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(sr => sr.Id == id, ct);
    }

    private async Task<SupplierReturnDto> MapToDtoAsync(SupplierReturn supplierReturn, CancellationToken ct)
    {
        var returnedByProduct = await GetReturnedQtyByProductAsync(supplierReturn.PurchaseOrderId, supplierReturn.Id, ct);
        var purchaseOrderItems = await _dbContext.PurchaseOrderItems
            .Where(item => item.PurchaseOrderId == supplierReturn.PurchaseOrderId)
            .ToDictionaryAsync(item => item.ProductId, ct);

        return new SupplierReturnDto(
            supplierReturn.Id,
            supplierReturn.ReturnNumber,
            supplierReturn.SupplierId,
            supplierReturn.Supplier?.Name ?? string.Empty,
            supplierReturn.PurchaseOrderId,
            supplierReturn.PurchaseOrder?.PoNumber ?? string.Empty,
            supplierReturn.PurchaseOrder?.OutletId ?? Guid.Empty,
            supplierReturn.PurchaseOrder?.Outlet?.Name ?? string.Empty,
            supplierReturn.ReturnDate,
            supplierReturn.TotalAmount,
            supplierReturn.Status,
            supplierReturn.Notes,
            supplierReturn.CreatedBy,
            supplierReturn.CreatedByUser?.Name ?? string.Empty,
            supplierReturn.Items.Select(item => new SupplierReturnItemDto(
                item.ProductId,
                item.Product?.Name ?? string.Empty,
                item.Product?.Sku ?? string.Empty,
                item.Qty,
                item.UnitCost,
                item.TotalCost,
                purchaseOrderItems.TryGetValue(item.ProductId, out var purchaseOrderItem)
                    ? Math.Max(0, purchaseOrderItem.Qty - returnedByProduct.GetValueOrDefault(item.ProductId))
                    : 0
            )).ToList()
        );
    }

    private static SupplierReturnListItemDto MapToListDto(SupplierReturn supplierReturn)
    {
        return new SupplierReturnListItemDto(
            supplierReturn.Id,
            supplierReturn.ReturnNumber,
            supplierReturn.SupplierId,
            supplierReturn.Supplier?.Name ?? string.Empty,
            supplierReturn.PurchaseOrderId,
            supplierReturn.PurchaseOrder?.PoNumber ?? string.Empty,
            supplierReturn.PurchaseOrder?.OutletId ?? Guid.Empty,
            supplierReturn.PurchaseOrder?.Outlet?.Name ?? string.Empty,
            supplierReturn.ReturnDate,
            supplierReturn.TotalAmount,
            supplierReturn.Status,
            supplierReturn.CreatedByUser?.Name ?? string.Empty
        );
    }

    private async Task EnsureOutletAccessibleAsync(Guid outletId, CancellationToken ct)
    {
        var outlet = await _dbContext.Outlets.AsNoTracking().FirstOrDefaultAsync(o => o.Id == outletId, ct);
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
