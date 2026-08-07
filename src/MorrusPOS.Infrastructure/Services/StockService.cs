using Microsoft.EntityFrameworkCore;
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
        CancellationToken ct = default)
    {
        // 1. Insert StockLedger
        var ledger = new StockLedger
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            OutletId = outletId,
            MovementType = movementType,
            QtyChange = qtyChange,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Note = note,
            CreatedBy = _currentUserService.UserId ?? Guid.Empty,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.StockLedgers.Add(ledger);

        // 2. Adjust InventoryStock (only for databases without trigger, e.g. InMemory unit tests)
        if (_dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            var stock = await _dbContext.InventoryStocks
                .FirstOrDefaultAsync(s => s.ProductId == productId && s.OutletId == outletId, ct);

            if (stock == null)
            {
                stock = new InventoryStock
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
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
}
