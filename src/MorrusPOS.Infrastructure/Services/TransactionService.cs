using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class TransactionService : ITransactionService
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPosNotificationService _notificationService;

    public TransactionService(
        AppDbContext dbContext,
        IStockService stockService,
        ICurrentUserService currentUserService,
        IPosNotificationService notificationService)
    {
        _dbContext = dbContext;
        _stockService = stockService;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
    }

    public async Task<TransactionDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var trx = await _dbContext.Transactions
            .Include(t => t.Outlet)
            .Include(t => t.User)
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .Include(t => t.Payments)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (trx == null)
        {
            throw new InvalidOperationException("Transaksi tidak ditemukan.");
        }

        return MapToDto(trx);
    }

    public async Task<TransactionDto> CheckoutAsync(CheckoutRequest request, CancellationToken ct = default)
    {
        // 1. Idempotency Check
        var existing = await _dbContext.Transactions
            .Include(t => t.Outlet)
            .Include(t => t.User)
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .Include(t => t.Payments)
            .FirstOrDefaultAsync(t => t.Id == request.Id, ct);

        if (existing != null)
        {
            return MapToDto(existing);
        }

        // 2. Validate Cashier Session
        var session = await _dbContext.CashierSessions.FindAsync(new object[] { request.CashierSessionId }, ct);
        if (session == null || session.Status != CashierSessionStatus.Open)
        {
            throw new InvalidOperationException("Sesi kasir tidak valid atau sudah ditutup.");
        }

        // 3. Atomicity: Execute inside database transaction
        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            // Validate stock levels before deducting
            foreach (var itemReq in request.Items)
            {
                var product = await _dbContext.Products.FindAsync(new object[] { itemReq.ProductId }, ct);
                if (product == null || !product.IsActive)
                {
                    throw new InvalidOperationException($"Produk tidak valid atau tidak aktif.");
                }

                var stock = await _dbContext.InventoryStocks
                    .FirstOrDefaultAsync(s => s.ProductId == itemReq.ProductId && s.OutletId == request.OutletId, ct);

                if (stock == null || stock.QtyOnHand < itemReq.Qty)
                {
                    throw new InvalidOperationException($"Stok tidak mencukupi untuk produk {product.Name}. Stok tersedia: {(stock != null ? stock.QtyOnHand : 0)}");
                }
            }

            // Generate unique transaction number without lock bottlenecks
            var rand = new Random();
            var trxNumber = $"TRX-{DateTime.UtcNow:yyyyMMddHHmmss}-{rand.Next(1000, 9999)}";

            var newTrx = new Transaction
            {
                Id = request.Id,
                OutletId = request.OutletId,
                UserId = _currentUserService.UserId ?? Guid.Empty,
                CashierSessionId = request.CashierSessionId,
                TransactionNumber = trxNumber,
                Channel = request.Channel,
                Status = TransactionStatus.Completed,
                Subtotal = request.Subtotal,
                DiscountTotal = request.DiscountTotal,
                TaxTotal = request.TaxTotal,
                GrandTotal = request.GrandTotal,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Transactions.Add(newTrx);

            // Add Items and record movement
            foreach (var itemReq in request.Items)
            {
                var product = await _dbContext.Products.FindAsync(new object[] { itemReq.ProductId }, ct);

                var item = new TransactionItem
                {
                    Id = Guid.NewGuid(),
                    TransactionId = newTrx.Id,
                    ProductId = itemReq.ProductId,
                    Qty = itemReq.Qty,
                    UnitPrice = itemReq.UnitPrice,
                    UnitCost = product!.CostPrice, // HPP snapshot
                    DiscountAmount = itemReq.DiscountAmount,
                    LineTotal = (itemReq.UnitPrice * itemReq.Qty) - itemReq.DiscountAmount,
                    IsReturned = false
                };

                _dbContext.TransactionItems.Add(item);

                // Add movement
                await _stockService.AddMovementAsync(
                    productId: itemReq.ProductId,
                    outletId: request.OutletId,
                    qtyChange: -itemReq.Qty,
                    movementType: StockMovementType.Sale,
                    referenceType: "transaction",
                    referenceId: newTrx.Id,
                    note: $"Penjualan kasir {trxNumber}",
                    ct: ct
                );
            }

            // Add Payments
            foreach (var payReq in request.Payments)
            {
                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    TransactionId = newTrx.Id,
                    Method = payReq.Method,
                    Amount = payReq.Amount,
                    ReferenceNumber = payReq.ReferenceNumber,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Payments.Add(payment);
            }

            await _dbContext.SaveChangesAsync(ct);
            await dbTx.CommitAsync(ct);

            // 4. Real-time updates broadcast to outlet group
            var stockUpdates = request.Items.Select(i => new StockUpdateItem(i.ProductId, i.Qty)).ToList();
            await _notificationService.SendStockUpdateAsync(request.OutletId, stockUpdates, ct);

            // Fetch fully populated entity for return mapping
            return await GetByIdAsync(newTrx.Id, ct);
        }
        catch (Exception)
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
    }

    private static TransactionDto MapToDto(Transaction t)
    {
        return new TransactionDto(
            t.Id,
            t.TransactionNumber,
            t.OutletId,
            t.Outlet?.Name ?? string.Empty,
            t.UserId,
            t.User?.Name ?? string.Empty,
            t.CashierSessionId,
            t.Channel,
            t.Status,
            t.Subtotal,
            t.DiscountTotal,
            t.TaxTotal,
            t.GrandTotal,
            t.CreatedAt,
            t.Items.Select(i => new TransactionItemDto(
                i.ProductId,
                i.Product?.Name ?? string.Empty,
                i.Product?.Sku ?? string.Empty,
                i.Qty,
                i.UnitPrice,
                i.UnitCost,
                i.DiscountAmount,
                i.LineTotal
            )).ToList(),
            t.Payments.Select(p => new PaymentDto(
                p.Method,
                p.Amount,
                p.ReferenceNumber,
                p.CreatedAt
            )).ToList()
        );
    }
}
