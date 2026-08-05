using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Consignments;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class ConsignmentService : IConsignmentService
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockService;
    private readonly IPosNotificationService _notificationService;
    private readonly ICurrentUserService _currentUser;

    public ConsignmentService(
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

    public async Task<ConsignmentDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var consignment = await _dbContext.Consignments
            .Include(c => c.Supplier)
            .Include(c => c.Outlet)
            .Include(c => c.CreatedByUser)
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (consignment == null)
        {
            throw new InvalidOperationException("Tanda terima konsinyasi tidak ditemukan.");
        }

        EnsureOutletAccess(consignment.OutletId);

        return MapToDto(consignment);
    }

    public async Task<IReadOnlyList<ConsignmentDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default)
    {
        EnsureOutletAccess(outletId);

        var consignments = await _dbContext.Consignments
            .Include(c => c.Supplier)
            .Include(c => c.Outlet)
            .Include(c => c.CreatedByUser)
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .Where(c => c.OutletId == outletId)
            .OrderByDescending(c => c.ReceiveDate)
            .ToListAsync(ct);

        return consignments.Select(MapToDto).ToList();
    }

    public async Task<ConsignmentDto> CreateAsync(Guid userId, CreateConsignmentRequest request, CancellationToken ct = default)
    {
        EnsureOutletAccess(request.OutletId);

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("Minimal harus ada satu item konsinyasi.");
        }

        // 1. Validate Supplier & Outlet
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

        // 2. Generate consignment number
        var rand = new Random();
        var consignmentNumber = $"CSG-{DateTime.UtcNow:yyyyMMddHHmmss}-{rand.Next(1000, 9999)}";

        // 3. Create consignment
        var consignment = new Consignment
        {
            Id = Guid.NewGuid(),
            SupplierId = request.SupplierId,
            OutletId = request.OutletId,
            ConsignmentNumber = consignmentNumber,
            ReceiveDate = DateTime.UtcNow,
            Status = ConsignmentStatus.Draft,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Consignments.Add(consignment);

        // 4. Add items
        foreach (var itemReq in request.Items)
        {
            if (itemReq.Qty <= 0)
            {
                throw new InvalidOperationException("Qty barang konsinyasi harus lebih dari 0.");
            }

            if (itemReq.UnitCost <= 0)
            {
                throw new InvalidOperationException("Unit cost barang konsinyasi harus lebih dari 0.");
            }

            if (itemReq.UnitPrice <= 0)
            {
                throw new InvalidOperationException("Unit price barang konsinyasi harus lebih dari 0.");
            }

            if (itemReq.UnitPrice < itemReq.UnitCost)
            {
                throw new InvalidOperationException("Harga jual tidak boleh kurang dari bagi hasil.");
            }

            var product = await _dbContext.Products.FindAsync(new object[] { itemReq.ProductId }, ct);
            if (product == null || !product.IsActive)
            {
                throw new InvalidOperationException($"Produk tidak valid atau tidak aktif.");
            }

            var item = new ConsignmentItem
            {
                Id = Guid.NewGuid(),
                ConsignmentId = consignment.Id,
                ProductId = itemReq.ProductId,
                Qty = itemReq.Qty,
                UnitCost = itemReq.UnitCost,
                UnitPrice = itemReq.UnitPrice
            };

            _dbContext.ConsignmentItems.Add(item);
        }

        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(consignment.Id, ct);
    }

    public async Task<ConsignmentDto> UpdateStatusAsync(Guid userId, Guid consignmentId, UpdateConsignmentStatusRequest request, CancellationToken ct = default)
    {
        var consignment = await _dbContext.Consignments
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.Id == consignmentId, ct);

        if (consignment == null)
        {
            throw new InvalidOperationException("Tanda terima konsinyasi tidak ditemukan.");
        }

        EnsureOutletAccess(consignment.OutletId);

        if (consignment.Status == ConsignmentStatus.Received || consignment.Status == ConsignmentStatus.Cancelled)
        {
            throw new InvalidOperationException($"Tanda terima konsinyasi sudah berstatus '{consignment.Status}', tidak bisa diubah kembali.");
        }

        var sanitizedStatus = request.Status?.Trim().ToLowerInvariant();
        var allowedStatuses = new[] { ConsignmentStatus.Received, ConsignmentStatus.Cancelled };
        if (!allowedStatuses.Contains(sanitizedStatus))
        {
            throw new InvalidOperationException($"Status '{request.Status}' tidak valid.");
        }

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var stockUpdates = new List<StockUpdateItem>();

            if (sanitizedStatus == ConsignmentStatus.Received)
            {
                foreach (var item in consignment.Items)
                {
                    // 1. Ensure product is flagged as consignment product
                    if (item.Product != null)
                    {
                        if (!item.Product.IsConsignment)
                        {
                            item.Product.IsConsignment = true;
                        }

                        // Update CostPrice (HPP) and log it if changed
                        if (item.Product.CostPrice != item.UnitCost)
                        {
                            var oldCost = item.Product.CostPrice;
                            item.Product.CostPrice = item.UnitCost;
                            item.Product.UpdatedAt = DateTime.UtcNow;

                            _dbContext.AuditLogs.Add(new AuditLog
                            {
                                Id = Guid.NewGuid(),
                                UserId = userId,
                                OutletId = consignment.OutletId,
                                EntityType = "product",
                                EntityId = item.ProductId,
                                Action = "cost_price_update",
                                OldValueJson = JsonSerializer.Serialize(new { CostPrice = oldCost }),
                                NewValueJson = JsonSerializer.Serialize(new { CostPrice = item.UnitCost, ConsignmentNumber = consignment.ConsignmentNumber }),
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }

                    // 2. Add stock movement (consignment_in)
                    await _stockService.AddMovementAsync(
                        productId: item.ProductId,
                        outletId: consignment.OutletId,
                        qtyChange: item.Qty,
                        movementType: StockMovementType.ConsignmentIn,
                        referenceType: "consignment",
                        referenceId: consignment.Id,
                        note: $"Penerimaan barang konsinyasi {consignment.ConsignmentNumber}",
                        ct: ct
                    );

                    stockUpdates.Add(new StockUpdateItem(item.ProductId, item.Qty));
                }
            }

            consignment.Status = sanitizedStatus!;
            consignment.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            await dbTx.CommitAsync(ct);

            // Broadcast real-time stock updates
            if (stockUpdates.Any())
            {
                await _notificationService.SendStockUpdateAsync(consignment.OutletId, stockUpdates, ct);
            }

            return await GetByIdAsync(consignment.Id, ct);
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

    private static ConsignmentDto MapToDto(Consignment c)
    {
        return new ConsignmentDto(
            c.Id,
            c.SupplierId,
            c.Supplier?.Name ?? string.Empty,
            c.OutletId,
            c.Outlet?.Name ?? string.Empty,
            c.ConsignmentNumber,
            c.ReceiveDate,
            c.Status,
            c.Items.Select(i => new ConsignmentItemDto(
                i.ProductId,
                i.Product?.Name ?? string.Empty,
                i.Product?.Sku ?? string.Empty,
                i.Qty,
                i.UnitCost,
                i.UnitPrice
            )).ToList()
        );
    }
}
