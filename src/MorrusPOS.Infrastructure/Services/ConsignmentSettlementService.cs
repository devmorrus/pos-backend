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

public class ConsignmentSettlementService : IConsignmentSettlementService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public ConsignmentSettlementService(AppDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ConsignmentSettlementDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var settlement = await _dbContext.ConsignmentSettlements
            .Include(s => s.Supplier)
            .Include(s => s.CreatedByUser)
            .Include(s => s.Sales).ThenInclude(sale => sale.TransactionItem).ThenInclude(ti => ti.Transaction)
            .Include(s => s.Sales).ThenInclude(sale => sale.TransactionItem).ThenInclude(ti => ti.Product)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (settlement == null)
        {
            throw new InvalidOperationException("Settlement konsinyasi tidak ditemukan.");
        }

        EnsureOutletAccess(settlement.OutletId);

        return MapToDto(settlement);
    }

    public async Task<IReadOnlyList<ConsignmentSettlementDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default)
    {
        EnsureOutletAccess(outletId);

        var settlements = await _dbContext.ConsignmentSettlements
            .Include(s => s.Supplier)
            .Include(s => s.Outlet)
            .Include(s => s.CreatedByUser)
            .Include(s => s.Sales).ThenInclude(sale => sale.TransactionItem).ThenInclude(ti => ti.Transaction)
            .Include(s => s.Sales).ThenInclude(sale => sale.TransactionItem).ThenInclude(ti => ti.Product)
            .Where(s => s.OutletId == outletId)
            .OrderByDescending(s => s.SettlementDate)
            .ToListAsync(ct);

        return settlements.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<ConsignmentSaleDto>> GetUnpaidSalesBySupplierAsync(Guid supplierId, Guid outletId, CancellationToken ct = default)
    {
        EnsureOutletAccess(outletId);

        var sales = await _dbContext.ConsignmentSales
            .Include(s => s.Supplier)
            .Include(s => s.TransactionItem).ThenInclude(ti => ti.Transaction)
            .Include(s => s.TransactionItem).ThenInclude(ti => ti.Product)
            .Where(s =>
                s.SupplierId == supplierId
                && s.Status == ConsignmentSaleStatus.Unpaid
                && s.ConsignmentSettlementId == null
                && s.TransactionItem.Transaction.OutletId == outletId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        return sales.Select(MapSaleToDto).ToList();
    }

    public async Task<ConsignmentSettlementDto> CreateSettlementAsync(Guid userId, CreateConsignmentSettlementRequest request, CancellationToken ct = default)
    {
        EnsureOutletAccess(request.OutletId);

        // 1. Validate Supplier
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

        // 2. Fetch all unpaid, unlinked sales
        var sales = await _dbContext.ConsignmentSales
            .Include(s => s.TransactionItem).ThenInclude(ti => ti.Transaction)
            .Where(s =>
                s.SupplierId == request.SupplierId
                && s.Status == ConsignmentSaleStatus.Unpaid
                && s.ConsignmentSettlementId == null
                && s.TransactionItem.Transaction.OutletId == request.OutletId)
            .ToListAsync(ct);

        if (!sales.Any())
        {
            throw new InvalidOperationException("Tidak ada penjualan konsinyasi yang belum diselesaikan untuk supplier ini.");
        }

        // 3. Generate settlement number
        var rand = new Random();
        var settlementNumber = $"SET-{DateTime.UtcNow:yyyyMMddHHmmss}-{rand.Next(1000, 9999)}";

        // 4. Create settlement
        var settlement = new ConsignmentSettlement
        {
            Id = Guid.NewGuid(),
            SupplierId = request.SupplierId,
            OutletId = request.OutletId,
            SettlementNumber = settlementNumber,
            SettlementDate = DateTime.UtcNow,
            TotalAmount = sales.Sum(s => s.TotalAmount),
            Status = ConsignmentSettlementStatus.Draft,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.ConsignmentSettlements.Add(settlement);

        // 5. Link sales to settlement
        foreach (var sale in sales)
        {
            sale.ConsignmentSettlementId = settlement.Id;
        }

        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(settlement.Id, ct);
    }

    public async Task<ConsignmentSettlementDto> UpdateStatusAsync(Guid userId, Guid settlementId, UpdateConsignmentSettlementStatusRequest request, CancellationToken ct = default)
    {
        var settlement = await _dbContext.ConsignmentSettlements
            .Include(s => s.Sales)
            .FirstOrDefaultAsync(s => s.Id == settlementId, ct);

        if (settlement == null)
        {
            throw new InvalidOperationException("Settlement konsinyasi tidak ditemukan.");
        }

        EnsureOutletAccess(settlement.OutletId);

        if (settlement.Status == ConsignmentSettlementStatus.Settled || settlement.Status == ConsignmentSettlementStatus.Cancelled)
        {
            throw new InvalidOperationException($"Settlement konsinyasi sudah berstatus '{settlement.Status}', tidak bisa diubah kembali.");
        }

        var sanitizedStatus = request.Status?.Trim().ToLowerInvariant();
        var allowedStatuses = new[] { ConsignmentSettlementStatus.Settled, ConsignmentSettlementStatus.Cancelled };
        if (!allowedStatuses.Contains(sanitizedStatus))
        {
            throw new InvalidOperationException($"Status '{request.Status}' tidak valid.");
        }

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            if (sanitizedStatus == ConsignmentSettlementStatus.Settled)
            {
                // Mark all linked sales as paid
                foreach (var sale in settlement.Sales)
                {
                    sale.Status = ConsignmentSaleStatus.Paid;
                }
            }
            else if (sanitizedStatus == ConsignmentSettlementStatus.Cancelled)
            {
                // Release all linked sales back to unpaid
                foreach (var sale in settlement.Sales)
                {
                    sale.ConsignmentSettlementId = null;
                }
            }

            settlement.Status = sanitizedStatus!;
            settlement.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            await dbTx.CommitAsync(ct);

            return await GetByIdAsync(settlement.Id, ct);
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

    private static ConsignmentSettlementDto MapToDto(ConsignmentSettlement s)
    {
        return new ConsignmentSettlementDto(
            s.Id,
            s.SupplierId,
            s.Supplier?.Name ?? string.Empty,
            s.OutletId,
            s.Outlet?.Name ?? string.Empty,
            s.SettlementNumber,
            s.SettlementDate,
            s.TotalAmount,
            s.Status,
            s.Sales.Select(MapSaleToDto).ToList()
        );
    }

    private static ConsignmentSaleDto MapSaleToDto(ConsignmentSale s)
    {
        return new ConsignmentSaleDto(
            s.Id,
            s.SupplierId,
            s.Supplier?.Name ?? string.Empty,
            s.TransactionItemId,
            s.TransactionItem?.Transaction?.TransactionNumber ?? string.Empty,
            s.TransactionItem?.Product?.Name ?? string.Empty,
            s.Qty,
            s.UnitCost,
            s.TotalAmount,
            s.Status,
            s.CreatedAt
        );
    }
}
