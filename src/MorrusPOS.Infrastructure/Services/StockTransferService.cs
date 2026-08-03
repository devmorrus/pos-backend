using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class StockTransferService : IStockTransferService
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockService;
    private readonly IPosNotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;

    public StockTransferService(
        AppDbContext dbContext,
        IStockService stockService,
        IPosNotificationService notificationService,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _stockService = stockService;
        _notificationService = notificationService;
        _currentUserService = currentUserService;
    }

    public async Task<StockTransferDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var transfer = await _dbContext.StockTransfers
            .Include(t => t.FromOutlet)
            .Include(t => t.ToOutlet)
            .Include(t => t.RequestedByUser)
            .Include(t => t.ApprovedByUser)
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (transfer == null)
        {
            throw new InvalidOperationException("Transfer Stok tidak ditemukan.");
        }

        return MapToDto(transfer);
    }

    public async Task<IReadOnlyList<StockTransferDto>> GetOutgoingTransfersAsync(Guid outletId, CancellationToken ct = default)
    {
        var transfers = await _dbContext.StockTransfers
            .Include(t => t.FromOutlet)
            .Include(t => t.ToOutlet)
            .Include(t => t.RequestedByUser)
            .Include(t => t.ApprovedByUser)
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .Where(t => t.FromOutletId == outletId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return transfers.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<StockTransferDto>> GetIncomingTransfersAsync(Guid outletId, CancellationToken ct = default)
    {
        var transfers = await _dbContext.StockTransfers
            .Include(t => t.FromOutlet)
            .Include(t => t.ToOutlet)
            .Include(t => t.RequestedByUser)
            .Include(t => t.ApprovedByUser)
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .Where(t => t.ToOutletId == outletId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return transfers.Select(MapToDto).ToList();
    }

    public async Task<StockTransferDto> CreateAsync(Guid userId, CreateStockTransferRequest request, CancellationToken ct = default)
    {
        if (request.FromOutletId == request.ToOutletId)
        {
            throw new InvalidOperationException("Outlet asal dan tujuan tidak boleh sama.");
        }

        var rand = new Random();
        var transferNumber = $"TRF-{DateTime.UtcNow:yyyyMMddHHmmss}-{rand.Next(1000, 9999)}";

        var transfer = new StockTransfer
        {
            Id = Guid.NewGuid(),
            FromOutletId = request.FromOutletId,
            ToOutletId = request.ToOutletId,
            TransferNumber = transferNumber,
            Status = StockTransferStatus.Pending,
            RequestedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.StockTransfers.Add(transfer);

        foreach (var itemReq in request.Items)
        {
            var product = await _dbContext.Products.FindAsync(new object[] { itemReq.ProductId }, ct);
            if (product == null || !product.IsActive)
            {
                throw new InvalidOperationException($"Produk tidak valid.");
            }

            var item = new StockTransferItem
            {
                Id = Guid.NewGuid(),
                StockTransferId = transfer.Id,
                ProductId = itemReq.ProductId,
                Qty = itemReq.Qty
            };

            _dbContext.StockTransferItems.Add(item);
        }

        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(transfer.Id, ct);
    }

    public async Task<StockTransferDto> ApproveAsync(Guid userId, Guid transferId, CancellationToken ct = default)
    {
        var transfer = await _dbContext.StockTransfers
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == transferId, ct);

        if (transfer == null)
        {
            throw new InvalidOperationException("Transfer Stok tidak ditemukan.");
        }

        if (transfer.Status != StockTransferStatus.Pending)
        {
            throw new InvalidOperationException("Transfer Stok sudah diproses sebelumnya.");
        }

        EnsureTransferApprovalAccess(transfer.ToOutletId);

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            // Validate stock levels in FromOutlet
            foreach (var item in transfer.Items)
            {
                var stock = await _dbContext.InventoryStocks
                    .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.OutletId == transfer.FromOutletId, ct);

                if (stock == null || stock.QtyOnHand < item.Qty)
                {
                    var product = await _dbContext.Products.FindAsync(new object[] { item.ProductId }, ct);
                    throw new InvalidOperationException($"Stok tidak mencukupi di outlet asal untuk produk {product?.Name ?? "Unknown"}.");
                }
            }

            // Deduct and Add stock
            foreach (var item in transfer.Items)
            {
                // Deduct from source
                await _stockService.AddMovementAsync(
                    productId: item.ProductId,
                    outletId: transfer.FromOutletId,
                    qtyChange: -item.Qty,
                    movementType: StockMovementType.TransferOut,
                    referenceType: "stock_transfer",
                    referenceId: transfer.Id,
                    note: $"Transfer keluar ke cabang via {transfer.TransferNumber}",
                    ct: ct
                );

                // Add to destination
                await _stockService.AddMovementAsync(
                    productId: item.ProductId,
                    outletId: transfer.ToOutletId,
                    qtyChange: item.Qty,
                    movementType: StockMovementType.TransferIn,
                    referenceType: "stock_transfer",
                    referenceId: transfer.Id,
                    note: $"Transfer masuk dari cabang via {transfer.TransferNumber}",
                    ct: ct
                );
            }

            transfer.Status = StockTransferStatus.Approved;
            transfer.ApprovedBy = userId;
            transfer.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            await dbTx.CommitAsync(ct);

            // Broadcast real-time stock updates to both groups
            var fromUpdates = transfer.Items.Select(i => new StockUpdateItem(i.ProductId, -i.Qty)).ToList();
            await _notificationService.SendStockUpdateAsync(transfer.FromOutletId, fromUpdates, ct);

            var toUpdates = transfer.Items.Select(i => new StockUpdateItem(i.ProductId, i.Qty)).ToList();
            await _notificationService.SendStockUpdateAsync(transfer.ToOutletId, toUpdates, ct);

            return await GetByIdAsync(transfer.Id, ct);
        }
        catch (Exception)
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<StockTransferDto> RejectAsync(Guid userId, Guid transferId, CancellationToken ct = default)
    {
        var transfer = await _dbContext.StockTransfers
            .FirstOrDefaultAsync(t => t.Id == transferId, ct);

        if (transfer == null)
        {
            throw new InvalidOperationException("Transfer Stok tidak ditemukan.");
        }

        if (transfer.Status != StockTransferStatus.Pending)
        {
            throw new InvalidOperationException("Transfer Stok sudah diproses sebelumnya.");
        }

        EnsureTransferApprovalAccess(transfer.ToOutletId);

        transfer.Status = StockTransferStatus.Rejected;
        transfer.ApprovedBy = userId;
        transfer.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(transfer.Id, ct);
    }

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
                i.Product?.Sku ?? string.Empty,
                i.Qty
            )).ToList()
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
}
