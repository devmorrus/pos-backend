using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Customers;
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
    private readonly IPricingService _pricingService;

    public TransactionService(
        AppDbContext dbContext,
        IStockService stockService,
        ICurrentUserService currentUserService,
        IPosNotificationService notificationService)
        : this(
            dbContext,
            stockService,
            currentUserService,
            notificationService,
            new PricingService(dbContext))
    {
    }

    public TransactionService(
        AppDbContext dbContext,
        IStockService stockService,
        ICurrentUserService currentUserService,
        IPosNotificationService notificationService,
        IPricingService pricingService)
    {
        _dbContext = dbContext;
        _stockService = stockService;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _pricingService = pricingService;
    }

    public async Task<IReadOnlyList<TransactionListItemDto>> GetRecentByOutletAsync(
        Guid outletId,
        int take,
        CancellationToken ct = default)
    {
        await EnsureTransactionReadRoleAsync();
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
        var trx = await GetTransactionAggregateAsync(id, ct);

        if (trx == null)
        {
            throw new InvalidOperationException("Transaksi tidak ditemukan.");
        }

        await EnsureTransactionReadRoleAsync();
        await EnsureOutletAccessibleAsync(trx.OutletId, ct);

        return MapToDto(trx);
    }

    public async Task<PricingBreakdownDto> PreviewPricingAsync(PricingPreviewRequest request, CancellationToken ct = default)
    {
        await EnsureOperationalRoleAsync();
        await EnsureOutletAccessibleAsync(request.OutletId, ct);
        return await _pricingService.CalculateAsync(request, ct);
    }

    public async Task<TransactionDto> VoidAsync(Guid id, VoidTransactionRequest request, CancellationToken ct = default)
    {
        await EnsureVoidRoleAsync();

        var transaction = await GetTransactionAggregateAsync(id, ct);
        if (transaction == null)
        {
            throw new InvalidOperationException("Transaksi tidak ditemukan.");
        }

        await EnsureOutletAccessibleAsync(transaction.OutletId, ct);

        if (transaction.Status == TransactionStatus.Voided)
        {
            return MapToDto(transaction);
        }

        if (transaction.Status != TransactionStatus.Completed)
        {
            throw new InvalidOperationException("Hanya transaksi completed yang dapat di-void.");
        }

        if (transaction.Returns.Count > 0)
        {
            throw new InvalidOperationException("Transaksi yang sudah memiliki refund tidak dapat di-void.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? throw new InvalidOperationException("Alasan void wajib diisi.")
            : request.Reason.Trim();

        var consignmentSales = await _dbContext.ConsignmentSales
            .Where(s => transaction.Items.Select(item => item.Id).Contains(s.TransactionItemId))
            .ToListAsync(ct);

        if (consignmentSales.Any(sale => sale.Status == ConsignmentSaleStatus.Paid || sale.ConsignmentSettlementId != null))
        {
            throw new InvalidOperationException("Transaksi konsinyasi yang sudah disettle tidak dapat di-void.");
        }

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var item in transaction.Items)
            {
                await _stockService.AddMovementAsync(
                    productId: item.ProductId,
                    outletId: transaction.OutletId,
                    qtyChange: item.Qty,
                    movementType: StockMovementType.Return,
                    referenceType: "transaction_void",
                    referenceId: transaction.Id,
                    note: $"Void transaksi {transaction.TransactionNumber}",
                    ct: ct
                );

                item.IsReturned = true;

                // Reverse Tempo PO SoldQty allocation (LIFO)
                if (item.Product != null && !item.Product.IsConsignment)
                {
                    var tempoPoItems = await _dbContext.PurchaseOrderItems
                        .Include(poi => poi.PurchaseOrder)
                        .Where(poi => poi.ProductId == item.ProductId &&
                                     poi.PurchaseOrder.OutletId == transaction.OutletId &&
                                     poi.PurchaseOrder.PaymentType == PurchaseOrderPaymentType.Tempo &&
                                     poi.PurchaseOrder.Status == PurchaseOrderStatus.Completed &&
                                     poi.SoldQty > 0)
                        .OrderByDescending(poi => poi.PurchaseOrder.PoDate)
                        .ToListAsync(ct);

                    decimal remainingToReverse = item.Qty;
                    foreach (var poItem in tempoPoItems)
                    {
                        if (remainingToReverse <= 0) break;

                        var reversible = Math.Min(remainingToReverse, poItem.SoldQty);
                        poItem.SoldQty -= reversible;
                        remainingToReverse -= reversible;
                    }
                }
            }

            if (consignmentSales.Count > 0)
            {
                _dbContext.ConsignmentSales.RemoveRange(consignmentSales);
            }

            transaction.Status = TransactionStatus.Voided;
            transaction.VoidedBy = _currentUserService.UserId;
            transaction.VoidedReason = reason;

            await _dbContext.SaveChangesAsync(ct);
            await dbTx.CommitAsync(ct);

            var stockUpdates = transaction.Items
                .Select(item => new StockUpdateItem(item.ProductId, item.Qty))
                .ToList();
            await _notificationService.SendStockUpdateAsync(transaction.OutletId, stockUpdates, ct);

            return await GetByIdAsync(transaction.Id, ct);
        }
        catch
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<TransactionDto> RefundAsync(Guid id, RefundTransactionRequest request, CancellationToken ct = default)
    {
        await EnsureRefundRoleAsync();

        var transaction = await GetTransactionAggregateAsync(id, ct);
        if (transaction == null)
        {
            throw new InvalidOperationException("Transaksi tidak ditemukan.");
        }

        await EnsureOutletAccessibleAsync(transaction.OutletId, ct);

        if (transaction.Status == TransactionStatus.Voided)
        {
            throw new InvalidOperationException("Transaksi yang sudah void tidak dapat direfund.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("Minimal satu item refund wajib diisi.");
        }

        var refundMethod = string.IsNullOrWhiteSpace(request.RefundMethod)
            ? throw new InvalidOperationException("Metode refund wajib diisi.")
            : request.RefundMethod.Trim().ToLowerInvariant();

        if (refundMethod is not "refund" and not "exchange")
        {
            throw new InvalidOperationException("Metode refund harus berupa refund atau exchange.");
        }

        var requestedProductIds = request.Items.Select(item => item.ProductId).ToHashSet();
        var consignmentSales = await _dbContext.ConsignmentSales
            .Where(s => transaction.Items
                .Where(item => requestedProductIds.Contains(item.ProductId))
                .Select(item => item.Id)
                .Contains(s.TransactionItemId))
            .ToListAsync(ct);

        if (consignmentSales.Any(sale => sale.Status == ConsignmentSaleStatus.Paid || sale.ConsignmentSettlementId != null))
        {
            throw new InvalidOperationException("Item konsinyasi yang sudah disettle tidak dapat direfund.");
        }

        var existingReturnedQtyByItemId = transaction.Returns
            .GroupBy(itemReturn => itemReturn.TransactionItemId)
            .ToDictionary(group => group.Key, group => group.Sum(itemReturn => itemReturn.Qty));

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var refundItem in request.Items)
            {
                var transactionItem = transaction.Items.FirstOrDefault(item => item.ProductId == refundItem.ProductId);
                if (transactionItem == null)
                {
                    throw new InvalidOperationException("Produk refund tidak ditemukan di transaksi.");
                }

                if (refundItem.Qty <= 0)
                {
                    throw new InvalidOperationException($"Qty refund untuk {transactionItem.Product.Name} harus lebih dari 0.");
                }

                var alreadyReturnedQty = existingReturnedQtyByItemId.GetValueOrDefault(transactionItem.Id);
                var remainingQty = transactionItem.Qty - alreadyReturnedQty;
                if (refundItem.Qty > remainingQty)
                {
                    throw new InvalidOperationException($"Qty refund {transactionItem.Product.Name} melebihi sisa qty yang belum direfund.");
                }

                var newReturn = new Return
                {
                    Id = Guid.NewGuid(),
                    TransactionId = transaction.Id,
                    TransactionItemId = transactionItem.Id,
                    Qty = refundItem.Qty,
                    Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                    RefundMethod = refundMethod,
                    ProcessedBy = _currentUserService.UserId ?? Guid.Empty,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Returns.Add(newReturn);

                await _stockService.AddMovementAsync(
                    productId: transactionItem.ProductId,
                    outletId: transaction.OutletId,
                    qtyChange: refundItem.Qty,
                    movementType: StockMovementType.Return,
                    referenceType: "transaction_refund",
                    referenceId: transaction.Id,
                    note: $"Refund transaksi {transaction.TransactionNumber}",
                    ct: ct
                );

                // Reverse Tempo PO SoldQty allocation (LIFO)
                if (transactionItem.Product != null && !transactionItem.Product.IsConsignment)
                {
                    var tempoPoItems = await _dbContext.PurchaseOrderItems
                        .Include(poi => poi.PurchaseOrder)
                        .Where(poi => poi.ProductId == transactionItem.ProductId &&
                                     poi.PurchaseOrder.OutletId == transaction.OutletId &&
                                     poi.PurchaseOrder.PaymentType == PurchaseOrderPaymentType.Tempo &&
                                     poi.PurchaseOrder.Status == PurchaseOrderStatus.Completed &&
                                     poi.SoldQty > 0)
                        .OrderByDescending(poi => poi.PurchaseOrder.PoDate)
                        .ToListAsync(ct);

                    decimal remainingToReverse = refundItem.Qty;
                    foreach (var poItem in tempoPoItems)
                    {
                        if (remainingToReverse <= 0) break;

                        var reversible = Math.Min(remainingToReverse, poItem.SoldQty);
                        poItem.SoldQty -= reversible;
                        remainingToReverse -= reversible;
                    }
                }

                var consignmentSale = consignmentSales.FirstOrDefault(sale => sale.TransactionItemId == transactionItem.Id);
                if (consignmentSale != null)
                {
                    if (refundItem.Qty == consignmentSale.Qty)
                    {
                        _dbContext.ConsignmentSales.Remove(consignmentSale);
                    }
                    else
                    {
                        consignmentSale.Qty -= refundItem.Qty;
                        consignmentSale.TotalAmount = consignmentSale.Qty * consignmentSale.UnitCost;
                    }
                }

                var nextReturnedQty = alreadyReturnedQty + refundItem.Qty;
                if (nextReturnedQty >= transactionItem.Qty)
                {
                    transactionItem.IsReturned = true;
                }

                existingReturnedQtyByItemId[transactionItem.Id] = nextReturnedQty;
            }

            if (transaction.Items.All(item => existingReturnedQtyByItemId.GetValueOrDefault(item.Id) >= item.Qty))
            {
                transaction.Status = TransactionStatus.Refunded;
            }

            await _dbContext.SaveChangesAsync(ct);
            await dbTx.CommitAsync(ct);

            var stockUpdates = request.Items
                .Select(item => new StockUpdateItem(item.ProductId, item.Qty))
                .ToList();
            await _notificationService.SendStockUpdateAsync(transaction.OutletId, stockUpdates, ct);

            return await GetByIdAsync(transaction.Id, ct);
        }
        catch
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
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
            var validatedItems = new List<ValidatedCheckoutItem>();
            Customer? selectedCustomer = null;

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

                validatedItems.Add(new ValidatedCheckoutItem(product, itemReq));
            }

            var pricingBreakdown = await _pricingService.CalculateAsync(new PricingPreviewRequest(
                request.OutletId,
                string.IsNullOrWhiteSpace(request.Channel) ? TransactionChannel.Pos : request.Channel,
                request.VoucherCode,
                request.AppliedPromoCode,
                request.Items
            ), ct);

            if (request.CustomerId.HasValue)
            {
                selectedCustomer = await _dbContext.Customers
                    .FirstOrDefaultAsync(customer => customer.Id == request.CustomerId.Value, ct);

                if (selectedCustomer == null)
                {
                    throw new InvalidOperationException("Customer tidak ditemukan.");
                }

                if (!selectedCustomer.IsActive)
                {
                    throw new InvalidOperationException("Customer yang dipilih sudah tidak aktif.");
                }
            }

            var totalPayment = request.Payments.Sum(payment => payment.Amount);
            if (totalPayment != pricingBreakdown.GrandTotal)
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
                CustomerId = selectedCustomer?.Id,
                TransactionNumber = trxNumber,
                Channel = string.IsNullOrWhiteSpace(request.Channel) ? TransactionChannel.Pos : request.Channel,
                Status = TransactionStatus.Completed,
                CustomerType = selectedCustomer != null
                    ? TransactionCustomerType.Member
                    : TransactionCustomerType.Guest,
                CustomerNameSnapshot = selectedCustomer?.Name,
                CustomerPhoneSnapshot = selectedCustomer?.Phone,
                Subtotal = pricingBreakdown.Subtotal,
                DiscountTotal = pricingBreakdown.ManualDiscountTotal + pricingBreakdown.PromoDiscountTotal + pricingBreakdown.VoucherDiscountTotal,
                ManualDiscountTotal = pricingBreakdown.ManualDiscountTotal,
                PromoDiscountTotal = pricingBreakdown.PromoDiscountTotal,
                VoucherDiscountTotal = pricingBreakdown.VoucherDiscountTotal,
                ServiceChargeTotal = pricingBreakdown.ServiceChargeTotal,
                TaxTotal = pricingBreakdown.TaxTotal,
                GrandTotal = pricingBreakdown.GrandTotal,
                AppliedVoucherCode = pricingBreakdown.AppliedVoucher?.Code,
                AppliedPromoName = pricingBreakdown.AppliedPromo?.Name,
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
                else
                {
                    // Allocate to Tempo PO items using FIFO
                    var tempoPoItems = await _dbContext.PurchaseOrderItems
                        .Include(poi => poi.PurchaseOrder)
                        .Where(poi => poi.ProductId == product.Id &&
                                     poi.PurchaseOrder.OutletId == request.OutletId &&
                                     poi.PurchaseOrder.PaymentType == PurchaseOrderPaymentType.Tempo &&
                                     poi.PurchaseOrder.Status == PurchaseOrderStatus.Completed &&
                                     poi.SoldQty < poi.Qty)
                        .OrderBy(poi => poi.PurchaseOrder.PoDate)
                        .ToListAsync(ct);

                    decimal remainingToAllocate = item.Qty;
                    foreach (var poItem in tempoPoItems)
                    {
                        if (remainingToAllocate <= 0) break;

                        var allocatable = Math.Min(remainingToAllocate, poItem.Qty - poItem.SoldQty);
                        poItem.SoldQty += allocatable;
                        remainingToAllocate -= allocatable;
                    }
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

            if (pricingBreakdown.AppliedVoucher is { } appliedVoucher)
            {
                var voucher = await _dbContext.Vouchers.FirstAsync(v => v.Id == appliedVoucher.VoucherId, ct);
                voucher.UsedCount += 1;
                _dbContext.VoucherRedemptions.Add(new VoucherRedemption
                {
                    Id = Guid.NewGuid(),
                    VoucherId = voucher.Id,
                    TransactionId = newTrx.Id,
                    RedeemedAt = DateTime.UtcNow,
                    RedeemedAmount = appliedVoucher.DiscountAmount
                });
            }

            if (selectedCustomer != null)
            {
                selectedCustomer.LifetimeSpend += pricingBreakdown.GrandTotal;
                selectedCustomer.LastTransactionAt = DateTime.UtcNow;
                selectedCustomer.UpdatedAt = DateTime.UtcNow;
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
            transaction.CustomerId,
            transaction.CustomerNameSnapshot,
            transaction.CustomerPhoneSnapshot,
            transaction.CustomerType,
            transaction.ExternalCustomerReference,
            transaction.CreatedAt,
            paymentSummary
        );
    }

    private static TransactionDto MapToDto(Transaction t)
    {
        var returnedQtyByItemId = t.Returns
            .GroupBy(itemReturn => itemReturn.TransactionItemId)
            .ToDictionary(group => group.Key, group => group.Sum(itemReturn => itemReturn.Qty));

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
            t.CustomerId,
            t.CustomerNameSnapshot ?? t.ExternalCustomerName,
            t.CustomerPhoneSnapshot ?? t.ExternalCustomerPhone,
            t.CustomerType,
            t.ExternalCustomerReference,
            t.ExternalCustomerName,
            t.ExternalCustomerPhone,
            t.Subtotal,
            t.DiscountTotal,
            t.ManualDiscountTotal,
            t.PromoDiscountTotal,
            t.VoucherDiscountTotal,
            t.ServiceChargeTotal,
            t.TaxTotal,
            t.GrandTotal,
            t.AppliedVoucherCode,
            t.AppliedPromoName,
            t.VoidedBy,
            t.VoidedByUser?.Name,
            t.VoidedReason,
            t.CreatedAt,
            new PricingBreakdownDto(
                t.Subtotal,
                t.ManualDiscountTotal,
                t.PromoDiscountTotal,
                t.VoucherDiscountTotal,
                t.ServiceChargeTotal,
                t.TaxTotal,
                t.GrandTotal,
                string.IsNullOrWhiteSpace(t.AppliedVoucherCode)
                    ? null
                    : new AppliedVoucherDto(Guid.Empty, t.AppliedVoucherCode, t.AppliedVoucherCode, t.VoucherDiscountTotal),
                string.IsNullOrWhiteSpace(t.AppliedPromoName)
                    ? null
                    : new AppliedPromoDto(Guid.Empty, null, t.AppliedPromoName, t.PromoDiscountTotal),
                t.Items.Select(i => new PricingLineBreakdownDto(
                    i.ProductId,
                    i.Product?.Name ?? string.Empty,
                    i.Qty,
                    i.UnitPrice * i.Qty,
                    i.DiscountAmount,
                    0,
                    0,
                    0,
                    0,
                    i.LineTotal
                )).ToList()
            ),
            t.Items.Select(i => new TransactionItemDto(
                i.Id,
                i.ProductId,
                i.Product?.Name ?? string.Empty,
                i.Product?.Sku ?? string.Empty,
                i.Qty,
                returnedQtyByItemId.GetValueOrDefault(i.Id),
                i.Qty - returnedQtyByItemId.GetValueOrDefault(i.Id),
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
            )).ToList(),
            t.Returns
                .OrderByDescending(itemReturn => itemReturn.CreatedAt)
                .Select(itemReturn => new TransactionReturnDto(
                    itemReturn.Id,
                    itemReturn.TransactionItemId,
                    itemReturn.TransactionItem.ProductId,
                    itemReturn.TransactionItem.Product?.Name ?? string.Empty,
                    itemReturn.Qty,
                    itemReturn.Reason,
                    itemReturn.RefundMethod,
                    itemReturn.ProcessedBy,
                    itemReturn.ProcessedByUser?.Name ?? string.Empty,
                    itemReturn.CreatedAt
                ))
                .ToList()
        );
    }

    private async Task<Transaction?> GetTransactionAggregateAsync(Guid id, CancellationToken ct)
    {
        return await _dbContext.Transactions
            .Include(t => t.Outlet)
            .Include(t => t.User)
            .Include(t => t.Customer)
            .Include(t => t.VoidedByUser)
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .Include(t => t.Payments)
            .Include(t => t.Returns).ThenInclude(r => r.TransactionItem).ThenInclude(ti => ti.Product)
            .Include(t => t.Returns).ThenInclude(r => r.ProcessedByUser)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    private Task EnsureOperationalRoleAsync()
    {
        if (_currentUserService.Role is "Owner" or "Admin" or "Kasir" or "KepalaCabang")
        {
            return Task.CompletedTask;
        }

        throw new UnauthorizedAccessException("Role Anda tidak memiliki akses ke POS kasir.");
    }

    private Task EnsureTransactionReadRoleAsync()
    {
        if (_currentUserService.Role is "Owner" or "Admin" or "Kasir" or "Keuangan" or "KepalaCabang")
        {
            return Task.CompletedTask;
        }

        throw new UnauthorizedAccessException("Role Anda tidak memiliki akses ke histori transaksi.");
    }

    private Task EnsureVoidRoleAsync()
    {
        if (_currentUserService.Role is "Owner" or "Admin")
        {
            return Task.CompletedTask;
        }

        throw new UnauthorizedAccessException("Role Anda tidak memiliki akses untuk void transaksi.");
    }

    private Task EnsureRefundRoleAsync()
    {
        if (_currentUserService.Role is "Owner" or "Admin" or "Kasir" or "KepalaCabang")
        {
            return Task.CompletedTask;
        }

        throw new UnauthorizedAccessException("Role Anda tidak memiliki akses untuk refund transaksi.");
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
