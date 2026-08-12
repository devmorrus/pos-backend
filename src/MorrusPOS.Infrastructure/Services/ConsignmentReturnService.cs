using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Consignments;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class ConsignmentReturnService : IConsignmentReturnService
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockService;
    private readonly IPosNotificationService _notificationService;
    private readonly ICurrentUserService _currentUser;

    public ConsignmentReturnService(
        AppDbContext dbContext,
        IStockService stockService,
        IPosNotificationService notificationService,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _stockService = stockService;
        _notificationService = notificationService;
        _currentUser = currentUser;
    }

    public async Task<ConsignmentReturnDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var ret = await _dbContext.ConsignmentReturns
            .Include(c => c.Supplier)
            .Include(c => c.Outlet)
            .Include(c => c.CreatedByUser)
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (ret == null)
        {
            throw new InvalidOperationException("Retur barang konsinyasi tidak ditemukan.");
        }

        EnsureOutletAccess(ret.OutletId);

        return MapToDto(ret);
    }

    public async Task<IReadOnlyList<ConsignmentReturnDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default)
    {
        EnsureOutletAccess(outletId);

        var returns = await _dbContext.ConsignmentReturns
            .Include(c => c.Supplier)
            .Include(c => c.Outlet)
            .Include(c => c.CreatedByUser)
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .Where(c => c.OutletId == outletId)
            .OrderByDescending(c => c.ReturnDate)
            .ToListAsync(ct);

        return returns.Select(MapToDto).ToList();
    }

    public async Task<ConsignmentReturnDto> CreateAsync(Guid userId, CreateConsignmentReturnRequest request, CancellationToken ct = default)
    {
        EnsureOutletAccess(request.OutletId);

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("Minimal harus ada satu item retur.");
        }

        var supplier = await _dbContext.Suppliers.FindAsync(new object[] { request.SupplierId }, ct);
        if (supplier == null || !supplier.IsActive)
        {
            throw new InvalidOperationException("Supplier tidak valid atau tidak aktif.");
        }

        var outlet = await _dbContext.Outlets.FindAsync(new object[] { request.OutletId }, ct);
        if (outlet == null || !outlet.IsActive)
        {
            throw new InvalidOperationException("Outlet tidak valid atau tidak aktif.");
        }

        var rand = new Random();
        var returnNumber = $"RTN-CSG-{DateTime.UtcNow:yyyyMMddHHmmss}-{rand.Next(1000, 9999)}";

        var consignmentReturn = new ConsignmentReturn
        {
            Id = Guid.NewGuid(),
            SupplierId = request.SupplierId,
            OutletId = request.OutletId,
            ReturnNumber = returnNumber,
            ReturnDate = DateTime.UtcNow,
            Status = ConsignmentReturnStatus.Draft,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.ConsignmentReturns.Add(consignmentReturn);

        foreach (var itemReq in request.Items)
        {
            if (itemReq.Qty <= 0)
            {
                throw new InvalidOperationException("Qty barang retur harus lebih dari 0.");
            }

            var product = await _dbContext.Products.FindAsync(new object[] { itemReq.ProductId }, ct);
            if (product == null || !product.IsActive)
            {
                throw new InvalidOperationException("Produk tidak valid atau tidak aktif.");
            }

            if (!product.IsConsignment)
            {
                throw new InvalidOperationException("Produk bukan merupakan produk konsinyasi.");
            }

            var item = new ConsignmentReturnItem
            {
                Id = Guid.NewGuid(),
                ConsignmentReturnId = consignmentReturn.Id,
                ProductId = itemReq.ProductId,
                Qty = itemReq.Qty
            };

            _dbContext.ConsignmentReturnItems.Add(item);
        }

        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(consignmentReturn.Id, ct);
    }

    public async Task<ConsignmentReturnDto> UpdateStatusAsync(Guid userId, Guid returnId, UpdateConsignmentReturnStatusRequest request, CancellationToken ct = default)
    {
        var consignmentReturn = await _dbContext.ConsignmentReturns
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.Id == returnId, ct);

        if (consignmentReturn == null)
        {
            throw new InvalidOperationException("Retur barang konsinyasi tidak ditemukan.");
        }

        EnsureOutletAccess(consignmentReturn.OutletId);

        if (consignmentReturn.Status == ConsignmentReturnStatus.Completed || consignmentReturn.Status == ConsignmentReturnStatus.Cancelled)
        {
            throw new InvalidOperationException($"Retur barang konsinyasi sudah berstatus '{consignmentReturn.Status}', tidak bisa diubah kembali.");
        }

        var sanitizedStatus = request.Status?.Trim().ToLowerInvariant();
        var allowedStatuses = new[] { ConsignmentReturnStatus.Completed, ConsignmentReturnStatus.Cancelled };
        if (!allowedStatuses.Contains(sanitizedStatus))
        {
            throw new InvalidOperationException($"Status '{request.Status}' tidak valid.");
        }

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var stockUpdates = new List<StockUpdateItem>();

            if (sanitizedStatus == ConsignmentReturnStatus.Completed)
            {
                foreach (var item in consignmentReturn.Items)
                {
                    // 1. Get current stock to verify availability
                    var inventory = await _dbContext.InventoryStocks
                        .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.OutletId == consignmentReturn.OutletId, ct);
                    
                    var currentStock = inventory?.QtyOnHand ?? 0;
                    if (currentStock < item.Qty)
                    {
                        throw new InvalidOperationException($"Stok produk '{item.Product?.Name}' tidak mencukupi untuk diretur (Stok tersedia: {currentStock}, Retur: {item.Qty}).");
                    }

                    // Find all active received consignment items for this product, supplier, and outlet that have remaining quantity
                    var consignmentItems = await _dbContext.ConsignmentItems
                        .Include(ci => ci.Consignment)
                        .Where(ci => ci.ProductId == item.ProductId &&
                                     ci.Consignment.SupplierId == consignmentReturn.SupplierId &&
                                     ci.Consignment.OutletId == consignmentReturn.OutletId &&
                                     ci.Consignment.Status == ConsignmentStatus.Received &&
                                     ci.Qty - ci.SoldQty - ci.ReturnedQty > 0)
                        .OrderBy(ci => ci.Consignment.ReceiveDate) // FIFO
                        .ToListAsync(ct);

                    decimal remainingToReturn = item.Qty;
                    foreach (var cItem in consignmentItems)
                    {
                        if (remainingToReturn <= 0) break;

                        var remainingAvailable = cItem.Qty - cItem.SoldQty - cItem.ReturnedQty;
                        var returnable = Math.Min(remainingToReturn, remainingAvailable);
                        cItem.ReturnedQty += returnable;
                        remainingToReturn -= returnable;
                    }

                    if (remainingToReturn > 0)
                    {
                        throw new InvalidOperationException($"Jumlah retur untuk '{item.Product?.Name}' melebihi kuantitas sisa yang belum terjual/retur pada tanda terima konsinyasi aktif (Kekurangan alokasi: {remainingToReturn}).");
                    }

                    // 2. Reduce stock via ledger (consignment_return)
                    await _stockService.AddMovementAsync(
                        productId: item.ProductId,
                        outletId: consignmentReturn.OutletId,
                        qtyChange: -item.Qty, // Negative for stock reduction
                        movementType: StockMovementType.ConsignmentReturn,
                        referenceType: "consignment_return",
                        referenceId: consignmentReturn.Id,
                        note: $"Retur barang konsinyasi {consignmentReturn.ReturnNumber}",
                        ct: ct
                    );

                    stockUpdates.Add(new StockUpdateItem(item.ProductId, -item.Qty));
                }
            }

            consignmentReturn.Status = sanitizedStatus!;
            consignmentReturn.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            await dbTx.CommitAsync(ct);

            if (stockUpdates.Any())
            {
                await _notificationService.SendStockUpdateAsync(consignmentReturn.OutletId, stockUpdates, ct);
            }

            return await GetByIdAsync(consignmentReturn.Id, ct);
        }
        catch (Exception)
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
    }

    private void EnsureOutletAccess(Guid outletId)
    {
        if (_currentUser.OutletId.HasValue && _currentUser.OutletId.Value != outletId)
        {
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke outlet tersebut.");
        }
    }

    private static ConsignmentReturnDto MapToDto(ConsignmentReturn r)
    {
        return new ConsignmentReturnDto(
            r.Id,
            r.SupplierId,
            r.Supplier?.Name ?? string.Empty,
            r.OutletId,
            r.Outlet?.Name ?? string.Empty,
            r.ReturnNumber,
            r.ReturnDate,
            r.Status,
            r.Items.Select(i => new ConsignmentReturnItemDto(
                i.ProductId,
                i.Product?.Name ?? string.Empty,
                i.Product?.Sku ?? string.Empty,
                i.Qty
            )).ToList()
        );
    }
}
