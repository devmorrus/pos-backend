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

    public async Task<IReadOnlyList<TransactionListItemDto>> GetRecentByOutletAsync(
        Guid outletId,
        int take,
        CancellationToken ct = default)
    {
        await EnsureOperationalRoleAsync();
        await EnsureOutletAccessibleAsync(outletId, ct);

        var transactions = await _dbContext.Transactions
            .Include(t => t.Outlet)
            .Include(t => t.User)
            .Include(t => t.Payments)
            .AsNoTracking()
            .Where(t => t.OutletId == outletId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(Math.Clamp(take, 1, 50))
            .ToListAsync(ct);

        return transactions.Select(MapToListItemDto).ToList();
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

        await EnsureOperationalRoleAsync();
        await EnsureOutletAccessibleAsync(trx.OutletId, ct);

        return MapToDto(trx);
    }

    public async Task<TransactionDto> CheckoutAsync(CheckoutRequest request, CancellationToken ct = default)
    {
        await EnsureOperationalRoleAsync();
        await EnsureOutletAccessibleAsync(request.OutletId, ct);

        if (_currentUserService.UserId == null)
        {
            throw new UnauthorizedAccessException("User tidak valid untuk checkout transaksi.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("Keranjang tidak boleh kosong.");
        }

        if (request.Payments == null || request.Payments.Count == 0)
        {
            throw new InvalidOperationException("Minimal satu pembayaran wajib diisi.");
        }

        var existing = await _dbContext.Transactions
            .Include(t => t.Outlet)
            .Include(t => t.User)
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .Include(t => t.Payments)
            .FirstOrDefaultAsync(t => t.Id == request.Id, ct);

        if (existing != null)
        {
            await EnsureOutletAccessibleAsync(existing.OutletId, ct);
            return MapToDto(existing);
        }

        var session = await _dbContext.CashierSessions
            .FirstOrDefaultAsync(s => s.Id == request.CashierSessionId, ct);

        if (session == null || session.Status != CashierSessionStatus.Open)
        {
            throw new InvalidOperationException("Sesi kasir tidak valid atau sudah ditutup.");
        }

        if (session.OutletId != request.OutletId)
        {
            throw new InvalidOperationException("Sesi kasir tidak cocok dengan outlet transaksi.");
        }

        if (_currentUserService.Role != "Owner")
        {
            if (_currentUserService.UserId != session.UserId || _currentUserService.OutletId != session.OutletId)
            {
                throw new UnauthorizedAccessException("Anda tidak dapat menggunakan sesi kasir ini.");
            }
        }

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            decimal recalculatedSubtotal = 0;
            decimal recalculatedDiscountTotal = 0;
            decimal recalculatedTaxTotal = 0;
            var validatedItems = new List<ValidatedCheckoutItem>();

            foreach (var itemReq in request.Items)
            {
                var product = await _dbContext.Products.FindAsync(new object[] { itemReq.ProductId }, ct);
                if (product == null || !product.IsActive)
                {
                    throw new InvalidOperationException("Produk tidak valid atau tidak aktif.");
                }

                if (itemReq.Qty <= 0)
                {
                    throw new InvalidOperationException($"Qty untuk produk {product.Name} harus lebih dari 0.");
                }

                if (itemReq.DiscountAmount < 0)
                {
                    throw new InvalidOperationException($"Diskon untuk produk {product.Name} tidak boleh negatif.");
                }

                if (itemReq.UnitPrice != product.BasePrice)
                {
                    throw new InvalidOperationException($"Harga produk {product.Name} sudah berubah. Silakan muat ulang data produk.");
                }

                var stock = await _dbContext.InventoryStocks
                    .FirstOrDefaultAsync(s => s.ProductId == itemReq.ProductId && s.OutletId == request.OutletId, ct);

                if (stock == null || stock.QtyOnHand < itemReq.Qty)
                {
                    throw new InvalidOperationException($"Stok tidak mencukupi untuk produk {product.Name}. Stok tersedia: {(stock != null ? stock.QtyOnHand : 0)}");
                }

                var lineSubtotal = product.BasePrice * itemReq.Qty;
                if (itemReq.DiscountAmount > lineSubtotal)
                {
                    throw new InvalidOperationException($"Diskon untuk produk {product.Name} melebihi nilai item.");
                }

                recalculatedSubtotal += lineSubtotal;
                recalculatedDiscountTotal += itemReq.DiscountAmount;
                validatedItems.Add(new ValidatedCheckoutItem(product, itemReq));
            }

            var recalculatedGrandTotal = recalculatedSubtotal - recalculatedDiscountTotal + recalculatedTaxTotal;

            if (request.Subtotal != recalculatedSubtotal ||
                request.DiscountTotal != recalculatedDiscountTotal ||
                request.TaxTotal != recalculatedTaxTotal ||
                request.GrandTotal != recalculatedGrandTotal)
            {
                throw new InvalidOperationException("Ringkasan transaksi tidak sinkron. Silakan muat ulang keranjang sebelum checkout.");
            }

            var totalPayment = request.Payments.Sum(payment => payment.Amount);
            if (totalPayment != recalculatedGrandTotal)
            {
                throw new InvalidOperationException("Total pembayaran harus sama dengan grand total transaksi.");
            }

            foreach (var payment in request.Payments)
            {
                if (payment.Amount <= 0)
                {
                    throw new InvalidOperationException("Nominal pembayaran harus lebih dari 0.");
                }

                if (!IsSupportedPaymentMethod(payment.Method))
                {
                    throw new InvalidOperationException($"Metode pembayaran {payment.Method} tidak didukung.");
                }
            }

            var rand = new Random();
            var trxNumber = $"TRX-{DateTime.UtcNow:yyyyMMddHHmmss}-{rand.Next(1000, 9999)}";

            var newTrx = new Transaction
            {
                Id = request.Id,
                OutletId = request.OutletId,
                UserId = _currentUserService.UserId.Value,
                CashierSessionId = request.CashierSessionId,
                TransactionNumber = trxNumber,
                Channel = string.IsNullOrWhiteSpace(request.Channel) ? TransactionChannel.Pos : request.Channel,
                Status = TransactionStatus.Completed,
                Subtotal = recalculatedSubtotal,
                DiscountTotal = recalculatedDiscountTotal,
                TaxTotal = recalculatedTaxTotal,
                GrandTotal = recalculatedGrandTotal,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Transactions.Add(newTrx);

            foreach (var validated in validatedItems)
            {
                var product = validated.Product;
                var itemReq = validated.Request;

                var isConsignment = product.IsConsignment;
                decimal itemUnitCost = product.CostPrice;
                Guid? consignmentSupplierId = null;

                if (isConsignment)
                {
                    var consignmentItem = await _dbContext.ConsignmentItems
                        .Include(ci => ci.Consignment)
                        .Where(ci => ci.ProductId == product.Id &&
                                     ci.Consignment.OutletId == request.OutletId &&
                                     ci.Consignment.Status == ConsignmentStatus.Received)
                        .OrderByDescending(ci => ci.Consignment.ReceiveDate)
                        .FirstOrDefaultAsync(ct);

                    if (consignmentItem == null)
                    {
                        consignmentItem = await _dbContext.ConsignmentItems
                            .Include(ci => ci.Consignment)
                            .Where(ci => ci.ProductId == product.Id && ci.Consignment.Status == ConsignmentStatus.Received)
                            .OrderByDescending(ci => ci.Consignment.ReceiveDate)
                            .FirstOrDefaultAsync(ct);
                    }

                    if (consignmentItem == null)
                    {
                        throw new InvalidOperationException($"Produk konsinyasi {product.Name} tidak memiliki tanda terima konsinyasi (status: received) yang aktif.");
                    }

                    itemUnitCost = consignmentItem.UnitCost;
                    consignmentSupplierId = consignmentItem.Consignment.SupplierId;
                }

                var item = new TransactionItem
                {
                    Id = Guid.NewGuid(),
                    TransactionId = newTrx.Id,
                    ProductId = product.Id,
                    Qty = itemReq.Qty,
                    UnitPrice = product.BasePrice,
                    UnitCost = itemUnitCost,
                    DiscountAmount = itemReq.DiscountAmount,
                    LineTotal = (product.BasePrice * itemReq.Qty) - itemReq.DiscountAmount,
                    IsReturned = false
                };

                _dbContext.TransactionItems.Add(item);

                if (isConsignment && consignmentSupplierId.HasValue)
                {
                    var consignmentSale = new ConsignmentSale
                    {
                        Id = Guid.NewGuid(),
                        SupplierId = consignmentSupplierId.Value,
                        TransactionItemId = item.Id,
                        Qty = item.Qty,
                        UnitCost = itemUnitCost,
                        TotalAmount = item.Qty * itemUnitCost,
                        Status = ConsignmentSaleStatus.Unpaid,
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.ConsignmentSales.Add(consignmentSale);
                }

                await _stockService.AddMovementAsync(
                    productId: product.Id,
                    outletId: request.OutletId,
                    qtyChange: -itemReq.Qty,
                    movementType: StockMovementType.Sale,
                    referenceType: "transaction",
                    referenceId: newTrx.Id,
                    note: $"Penjualan kasir {trxNumber}",
                    ct: ct
                );
            }

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

            var stockUpdates = request.Items.Select(i => new StockUpdateItem(i.ProductId, i.Qty)).ToList();
            await _notificationService.SendStockUpdateAsync(request.OutletId, stockUpdates, ct);

            return await GetByIdAsync(newTrx.Id, ct);
        }
        catch
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
    }

    private static TransactionListItemDto MapToListItemDto(Transaction transaction)
    {
        var firstPayment = transaction.Payments.FirstOrDefault();
        var paymentSummary = transaction.Payments.Count switch
        {
            0 => "-",
            1 => firstPayment?.Method ?? "-",
            _ => $"{firstPayment?.Method ?? "-"} +{transaction.Payments.Count - 1}",
        };

        return new TransactionListItemDto(
            transaction.Id,
            transaction.TransactionNumber,
            transaction.OutletId,
            transaction.Outlet?.Name ?? string.Empty,
            transaction.UserId,
            transaction.User?.Name ?? string.Empty,
            transaction.GrandTotal,
            transaction.Status,
            transaction.Channel,
            transaction.CreatedAt,
            paymentSummary
        );
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

    private Task EnsureOperationalRoleAsync()
    {
        if (_currentUserService.Role is "Owner" or "Admin" or "Kasir")
        {
            return Task.CompletedTask;
        }

        throw new UnauthorizedAccessException("Role Anda tidak memiliki akses ke POS kasir.");
    }

    private async Task EnsureOutletAccessibleAsync(Guid outletId, CancellationToken ct)
    {
        var outlet = await _dbContext.Outlets
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == outletId, ct);

        if (outlet == null || !outlet.IsActive)
        {
            throw new InvalidOperationException("Outlet tidak valid atau tidak aktif.");
        }

        if (_currentUserService.Role != "Owner" && _currentUserService.OutletId != outletId)
        {
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke outlet tersebut.");
        }
    }

    private static bool IsSupportedPaymentMethod(string method)
        => method is PaymentMethod.Cash or PaymentMethod.Qris or PaymentMethod.Transfer or PaymentMethod.Edc;
}

file sealed record ValidatedCheckoutItem(Product Product, CheckoutItemRequest Request);
