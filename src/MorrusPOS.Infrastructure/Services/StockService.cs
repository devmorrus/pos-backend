using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class StockService : IStockService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public StockService(AppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task AddMovementAsync(
        Guid productId,
        Guid outletId,
        decimal qtyChange,
        string movementType,
        string referenceType,
        Guid referenceId,
        string? note = null,
        CancellationToken ct = default,
        Guid? productVariantId = null,
        Guid? productBatchId = null,
        Guid? productSerialId = null,
        Guid? documentItemId = null,
        Guid? operationId = null)
    {
        ValidateMovement(qtyChange, movementType, productSerialId);
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User wajib tersedia untuk mencatat pergerakan stok.");
        var product = await _dbContext.Products.AsNoTracking()
            .Where(x => x.Id == productId)
            .Select(x => new { x.Id, x.TrackingMode })
            .SingleOrDefaultAsync(ct)
            ?? throw new StockIntegrityException(StockIntegrityErrorCodes.MovementInvalid, "Produk pergerakan stok tidak ditemukan.");
        if (product.TrackingMode == ProductTrackingModes.None)
            throw new StockIntegrityException(StockIntegrityErrorCodes.MovementInvalid,
                "Produk tanpa tracking stok tidak boleh memiliki ledger.");

        var postingKey = BuildPostingKey(productId, outletId, movementType, referenceType,
            referenceId, operationId ?? documentItemId ?? referenceId,
            productVariantId, productBatchId, productSerialId);
        var local = _dbContext.StockLedgers.Local.FirstOrDefault(x => x.PostingKey == postingKey);
        var existing = local ?? await _dbContext.StockLedgers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PostingKey == postingKey, ct);
        if (existing is not null)
        {
            if (existing.ProductId != productId || existing.ProductVariantId != productVariantId ||
                existing.OutletId != outletId || existing.ProductBatchId != productBatchId ||
                existing.ProductSerialId != productSerialId || existing.MovementType != movementType ||
                existing.QtyChange != qtyChange || existing.ReferenceType != referenceType ||
                existing.ReferenceId != referenceId)
                throw new StockIntegrityException(StockIntegrityErrorCodes.PostingConflict,
                    "Posting key stok sudah digunakan oleh pergerakan yang berbeda.");
            return;
        }

        // 1. Insert StockLedger
        var ledger = new StockLedger
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductVariantId = productVariantId,
            OutletId = outletId,
            ProductBatchId = productBatchId,
            ProductSerialId = productSerialId,
            DocumentItemId = documentItemId,
            PostingKey = postingKey,
            IsSystemGenerated = false,
            MovementType = movementType,
            QtyChange = qtyChange,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Note = note,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.StockLedgers.Add(ledger);

        // 2. Adjust InventoryStock (only for databases without trigger, e.g. InMemory unit tests)
        if (_dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            var stock = await _dbContext.InventoryStocks
                .FirstOrDefaultAsync(s => s.ProductId == productId && s.ProductVariantId == productVariantId && s.OutletId == outletId, ct);

            if (stock == null)
            {
                stock = new InventoryStock
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    ProductVariantId = productVariantId,
                    OutletId = outletId,
                    QtyOnHand = qtyChange,
                    MinStockAlert = 0,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.InventoryStocks.Add(stock);
            }
            else
            {
                stock.QtyOnHand += qtyChange;
                stock.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private static void ValidateMovement(decimal qtyChange, string movementType, Guid? productSerialId)
    {
        if (qtyChange == 0 || !StockMovementType.All.Contains(movementType))
            throw new StockIntegrityException(StockIntegrityErrorCodes.MovementInvalid,
                "Pergerakan stok harus memiliki tipe valid dan qty nonzero.");
        if (movementType is StockMovementType.Sale or StockMovementType.TransferOut or StockMovementType.ConsignmentReturn && qtyChange >= 0)
            throw new StockIntegrityException(StockIntegrityErrorCodes.MovementInvalid,
                "Pergerakan stok keluar harus bernilai negatif.");
        if (movementType is StockMovementType.Return or StockMovementType.PurchaseIn or StockMovementType.TransferIn or StockMovementType.ConsignmentIn && qtyChange <= 0)
            throw new StockIntegrityException(StockIntegrityErrorCodes.MovementInvalid,
                "Pergerakan stok masuk harus bernilai positif.");
        if (productSerialId.HasValue && Math.Abs(qtyChange) != 1)
            throw new StockIntegrityException(StockIntegrityErrorCodes.MovementInvalid,
                "Pergerakan serial harus tepat satu unit.");
    }

    private static string BuildPostingKey(
        Guid productId, Guid outletId, string movementType, string referenceType,
        Guid referenceId, Guid operationId, Guid? variantId, Guid? batchId, Guid? serialId)
        => string.Join('/', referenceType, referenceId.ToString("N"), movementType,
            operationId.ToString("N"), outletId.ToString("N"), productId.ToString("N"),
            variantId?.ToString("N") ?? "base", batchId?.ToString("N") ?? "no-batch",
            serialId?.ToString("N") ?? "no-serial");
}
