using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Consignments;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class ConsignmentSettlementService : IConsignmentSettlementService
{
    private readonly AppDbContext _dbContext;

    public ConsignmentSettlementService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
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

        return MapToDto(settlement);
    }

    public async Task<IReadOnlyList<ConsignmentSettlementDto>> GetSettlementsBySupplierAsync(Guid supplierId, CancellationToken ct = default)
    {
        var settlements = await _dbContext.ConsignmentSettlements
            .Include(s => s.Supplier)
            .Include(s => s.CreatedByUser)
            .Include(s => s.Sales).ThenInclude(sale => sale.TransactionItem).ThenInclude(ti => ti.Transaction)
            .Include(s => s.Sales).ThenInclude(sale => sale.TransactionItem).ThenInclude(ti => ti.Product)
            .Where(s => s.SupplierId == supplierId)
            .OrderByDescending(s => s.SettlementDate)
            .ToListAsync(ct);

        return settlements.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<ConsignmentSaleDto>> GetUnpaidSalesBySupplierAsync(Guid supplierId, CancellationToken ct = default)
    {
        var sales = await _dbContext.ConsignmentSales
            .Include(s => s.Supplier)
            .Include(s => s.TransactionItem).ThenInclude(ti => ti.Transaction)
            .Include(s => s.TransactionItem).ThenInclude(ti => ti.Product)
            .Where(s => s.SupplierId == supplierId && s.Status == ConsignmentSaleStatus.Unpaid && s.ConsignmentSettlementId == null)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        return sales.Select(MapSaleToDto).ToList();
    }

    public async Task<ConsignmentSettlementDto> CreateSettlementAsync(Guid userId, CreateConsignmentSettlementRequest request, CancellationToken ct = default)
    {
        // 1. Validate Supplier
        var supplier = await _dbContext.Suppliers.FindAsync(new object[] { request.SupplierId }, ct);
        if (supplier == null || !supplier.IsActive)
        {
            throw new InvalidOperationException("Supplier tidak valid atau tidak aktif.");
        }

        // 2. Fetch all unpaid, unlinked sales
        var sales = await _dbContext.ConsignmentSales
            .Where(s => s.SupplierId == request.SupplierId && s.Status == ConsignmentSaleStatus.Unpaid && s.ConsignmentSettlementId == null)
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

        if (settlement.Status == ConsignmentSettlementStatus.Settled || settlement.Status == ConsignmentSettlementStatus.Cancelled)
        {
            throw new InvalidOperationException($"Settlement konsinyasi sudah berstatus '{settlement.Status}', tidak bisa diubah kembali.");
        }

        var allowedStatuses = new[] { ConsignmentSettlementStatus.Settled, ConsignmentSettlementStatus.Cancelled };
        if (!allowedStatuses.Contains(request.Status))
        {
            throw new InvalidOperationException($"Status '{request.Status}' tidak valid.");
        }

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            if (request.Status == ConsignmentSettlementStatus.Settled)
            {
                // Mark all linked sales as paid
                foreach (var sale in settlement.Sales)
                {
                    sale.Status = ConsignmentSaleStatus.Paid;
                }
            }
            else if (request.Status == ConsignmentSettlementStatus.Cancelled)
            {
                // Release all linked sales back to unpaid
                foreach (var sale in settlement.Sales)
                {
                    sale.ConsignmentSettlementId = null;
                }
            }

            settlement.Status = request.Status;
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

    private static ConsignmentSettlementDto MapToDto(ConsignmentSettlement s)
    {
        return new ConsignmentSettlementDto(
            s.Id,
            s.SupplierId,
            s.Supplier?.Name ?? string.Empty,
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
