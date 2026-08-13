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

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
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

                // Validate stock levels in FromOutlet immediately on creation
                var stock = await _dbContext.InventoryStocks
                    .FirstOrDefaultAsync(s => s.ProductId == itemReq.ProductId && s.OutletId == request.FromOutletId, ct);

                if (stock == null || stock.QtyOnHand < itemReq.Qty)
                {
                    throw new InvalidOperationException($"Stok tidak mencukupi di outlet asal untuk produk {product.Name}. (Stok tersedia: {stock?.QtyOnHand ?? 0}, Dibutuhkan: {itemReq.Qty})");
                }

                var item = new StockTransferItem
                {
                    Id = Guid.NewGuid(),
                    StockTransferId = transfer.Id,
                    ProductId = itemReq.ProductId,
                    Qty = itemReq.Qty
                };

                _dbContext.StockTransferItems.Add(item);

                // Deduct stock from source immediately (TransferOut)
                await _stockService.AddMovementAsync(
                    productId: itemReq.ProductId,
                    outletId: request.FromOutletId,
                    qtyChange: -itemReq.Qty,
                    movementType: StockMovementType.TransferOut,
                    referenceType: "stock_transfer",
                    referenceId: transfer.Id,
                    note: $"Transfer keluar ke cabang via {transferNumber}",
                    ct: ct
                );
            }

            await _dbContext.SaveChangesAsync(ct);
            await dbTx.CommitAsync(ct);

            // Broadcast real-time stock updates to source outlet
            var fromUpdates = request.Items.Select(i => new StockUpdateItem(i.ProductId, -i.Qty)).ToList();
            await _notificationService.SendStockUpdateAsync(request.FromOutletId, fromUpdates, ct);

            return await GetByIdAsync(transfer.Id, ct);
        }
        catch (Exception)
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
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
            // Add stock to destination branch
            foreach (var item in transfer.Items)
            {
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

            // Broadcast real-time stock updates to destination outlet
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
            // Restore/Return stock to FromOutlet
            foreach (var item in transfer.Items)
            {
                await _stockService.AddMovementAsync(
                    productId: item.ProductId,
                    outletId: transfer.FromOutletId,
                    qtyChange: item.Qty, // Add back
                    movementType: StockMovementType.TransferIn, // Put it back as TransferIn
                    referenceType: "stock_transfer",
                    referenceId: transfer.Id,
                    note: $"Pengembalian stok akibat transfer ditolak {transfer.TransferNumber}",
                    ct: ct
                );
            }

            transfer.Status = StockTransferStatus.Rejected;
            transfer.ApprovedBy = userId;
            transfer.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            await dbTx.CommitAsync(ct);

            // Broadcast real-time stock updates to source outlet
            var fromUpdates = transfer.Items.Select(i => new StockUpdateItem(i.ProductId, i.Qty)).ToList();
            await _notificationService.SendStockUpdateAsync(transfer.FromOutletId, fromUpdates, ct);

            return await GetByIdAsync(transfer.Id, ct);
        }
        catch (Exception)
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
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
