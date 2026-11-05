using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Application.Features.Customers;
using MorrusPOS.Application.Features.Capabilities;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using System.Security.Cryptography;
using System.Text;

namespace MorrusPOS.Infrastructure.Services;

public class TransactionService : ITransactionService
{
    private readonly AppDbContext _dbContext;
    private readonly IStockService _stockService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPosNotificationService _notificationService;
    private readonly IPricingService _pricingService;
    private readonly IAccountingIntegrationService? _accountingIntegrationService;
    private readonly ICapabilityEvaluator? _capabilityEvaluator;
    private readonly IIdempotencyService? _idempotencyService;

    public TransactionService(
        AppDbContext dbContext,
        IStockService stockService,
        ICurrentUserService currentUserService,
        IPosNotificationService notificationService,
        IAccountingIntegrationService? accountingIntegrationService = null,
        IIdempotencyService? idempotencyService = null)
        : this(
            dbContext,
            stockService,
            currentUserService,
            notificationService,
            new PricingService(dbContext),
            accountingIntegrationService,
            null,
            idempotencyService)
    {
    }

    public TransactionService(
        AppDbContext dbContext,
        IStockService stockService,
        ICurrentUserService currentUserService,
        IPosNotificationService notificationService,
        IPricingService pricingService,
        IAccountingIntegrationService? accountingIntegrationService = null,
        ICapabilityEvaluator? capabilityEvaluator = null,
        IIdempotencyService? idempotencyService = null)
    {
        _dbContext = dbContext;
        _stockService = stockService;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _pricingService = pricingService;
        _accountingIntegrationService = accountingIntegrationService;
        _capabilityEvaluator = capabilityEvaluator;
        _idempotencyService = idempotencyService;
    }

    public async Task<IReadOnlyList<TransactionListItemDto>> GetRecentByOutletAsync(
        Guid outletId,
        int take,
        CancellationToken ct = default)
    {
        await EnsureTransactionReadRoleAsync();
        await EnsureOutletAccessibleAsync(outletId, ct);
        if (_capabilityEvaluator is not null)
            await _capabilityEvaluator.EnsureEnabledAsync(CapabilityCodes.SalesTransactions, outletId, ct);

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
        if (_capabilityEvaluator is not null)
            await _capabilityEvaluator.EnsureEnabledAsync(CapabilityCodes.SalesTransactions, trx.OutletId, ct);

        return MapToDto(trx, await GetItemAllocationsAsync(trx.Id, trx.Items.Select(i => i.Id), ct));
    }

    public async Task<PricingBreakdownDto> PreviewPricingAsync(PricingPreviewRequest request, CancellationToken ct = default)
    {
        if (_capabilityEvaluator is not null)
            await _capabilityEvaluator.EnsureEnabledAsync(CapabilityCodes.SalesPos, request.OutletId, ct);
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
        if (_capabilityEvaluator is not null)
            await _capabilityEvaluator.EnsureEnabledAsync(CapabilityCodes.SalesTransactions, transaction.OutletId, ct);

        if (transaction.Status == TransactionStatus.Voided)
        {
            return await GetByIdAsync(transaction.Id, ct);
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
                await RestoreTrackedStockAsync(item, transaction.OutletId, item.Qty,
                    transaction.Id, item.Id, "transaction_void",
                    $"Void transaksi {transaction.TransactionNumber}", null, null, item.Id, ct);

                item.IsReturned = true;

                // Reverse Consignment/Tempo PO SoldQty allocation (LIFO)
                if (item.Product != null)
                {
                    if (item.Product.IsConsignment)
                    {
                        var consignmentItems = await _dbContext.ConsignmentItems
                            .Include(ci => ci.Consignment)
                            .Where(ci => ci.ProductId == item.ProductId &&
                                         ci.Consignment.OutletId == transaction.OutletId &&
                                         ci.Consignment.Status == ConsignmentStatus.Received &&
                                         ci.SoldQty > 0)
                            .OrderByDescending(ci => ci.Consignment.ReceiveDate)
                            .ToListAsync(ct);

                        decimal remainingToReverse = item.Qty;
                        foreach (var cItem in consignmentItems)
                        {
                            if (remainingToReverse <= 0) break;

                            var reversible = Math.Min(remainingToReverse, cItem.SoldQty);
                            cItem.SoldQty -= reversible;
                            remainingToReverse -= reversible;
                        }
                    }
                    else
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
            }

            if (consignmentSales.Count > 0)
            {
                _dbContext.ConsignmentSales.RemoveRange(consignmentSales);
            }

            transaction.Status = TransactionStatus.Voided;
            transaction.VoidedBy = _currentUserService.UserId;
            transaction.VoidedReason = reason;

            await _dbContext.SaveChangesAsync(ct);
            if (_accountingIntegrationService != null)
                await _accountingIntegrationService.EnsureVoidPostedAsync(transaction.Id, ct);
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

        if (request.Id == Guid.Empty)
            throw new IdempotencyException(IdempotencyErrorCodes.KeyRequired,
                "Refund request ID wajib tersedia.");
        var requestHash = ComputeRequestHash(request);
        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        IdempotencyClaim? idempotencyClaim = null;
        if (_idempotencyService is not null)
        {
            idempotencyClaim = await _idempotencyService.ClaimAsync(
                IdempotencyOperation.Refund, request.Id, requestHash,
                _currentUserService.UserId ?? throw new UnauthorizedAccessException("User refund tidak valid."),
                _currentUserService.OutletId, ct);
            if (idempotencyClaim.Replayed)
            {
                await dbTx.CommitAsync(ct);
                return await GetByIdAsync(idempotencyClaim.ResourceId!.Value, ct);
            }
        }
        if (_dbContext.Database.IsRelational())
        {
            var businessId = _currentUserService.BusinessId
                ?? throw new UnauthorizedAccessException("Business refund tidak valid.");
            await _dbContext.Database.SqlQuery<int>(
                $"SELECT 1 AS \"Value\" FROM (SELECT pg_advisory_xact_lock(hashtextextended({$"{businessId:N}:refund-resource:{id:N}"},0))) lock_row")
                .SingleAsync(ct);
        }

        var transaction = await GetTransactionAggregateAsync(id, ct);
        if (transaction == null)
        {
            throw new InvalidOperationException("Transaksi tidak ditemukan.");
        }

        await EnsureOutletAccessibleAsync(transaction.OutletId, ct);
        if (_capabilityEvaluator is not null)
            await _capabilityEvaluator.EnsureEnabledAsync(CapabilityCodes.SalesTransactions, transaction.OutletId, ct);

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
        var requestedTransactionItemIds = transaction.Items
            .Where(item => requestedProductIds.Contains(item.ProductId))
            .Select(item => item.Id)
            .ToHashSet();
        var consignmentSales = await _dbContext.ConsignmentSales
            .Where(s => requestedTransactionItemIds.Contains(s.TransactionItemId))
            .ToListAsync(ct);

        if (consignmentSales.Any(sale => sale.Status == ConsignmentSaleStatus.Paid || sale.ConsignmentSettlementId != null))
        {
            throw new InvalidOperationException("Item konsinyasi yang sudah disettle tidak dapat direfund.");
        }

        var existingReturnedQtyByItemId = transaction.Returns
            .GroupBy(itemReturn => itemReturn.TransactionItemId)
            .ToDictionary(group => group.Key, group => group.Sum(itemReturn => itemReturn.Qty));

        try
        {
            foreach (var refundItem in request.Items)
            {
                var transactionItem = refundItem.TransactionItemId.HasValue
                    ? transaction.Items.FirstOrDefault(item => item.Id == refundItem.TransactionItemId)
                    : transaction.Items.FirstOrDefault(item => item.ProductId == refundItem.ProductId &&
                        item.ProductVariantId == refundItem.ProductVariantId);
                if (transactionItem == null)
                {
                    throw new InvalidOperationException("Produk refund tidak ditemukan di transaksi.");
                }
                if (transactionItem.ProductId != refundItem.ProductId ||
                    transactionItem.ProductVariantId != refundItem.ProductVariantId)
                {
                    throw new TrackingValidationException(TrackingErrorCodes.TrackingAllocationMismatch,
                        "Item refund tidak cocok dengan produk atau varian transaksi.");
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
                    RefundRequestId = request.Id,
                    Qty = refundItem.Qty,
                    Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                    RefundMethod = refundMethod,
                    ProcessedBy = _currentUserService.UserId ?? Guid.Empty,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Returns.Add(newReturn);

                await RestoreTrackedStockAsync(transactionItem, transaction.OutletId, refundItem.Qty,
                    transaction.Id, transactionItem.Id, "transaction_refund",
                    $"Refund transaksi {transaction.TransactionNumber}",
                    refundItem.BatchAllocations, refundItem.SerialIds, newReturn.Id, ct);

                // Reverse Consignment/Tempo PO SoldQty allocation (LIFO)
                if (transactionItem.Product != null)
                {
                    if (transactionItem.Product.IsConsignment)
                    {
                        var consignmentItems = await _dbContext.ConsignmentItems
                            .Include(ci => ci.Consignment)
                            .Where(ci => ci.ProductId == transactionItem.ProductId &&
                                         ci.Consignment.OutletId == transaction.OutletId &&
                                         ci.Consignment.Status == ConsignmentStatus.Received &&
                                         ci.SoldQty > 0)
                            .OrderByDescending(ci => ci.Consignment.ReceiveDate) // LIFO for reverse
                            .ToListAsync(ct);

                        decimal remainingToReverse = refundItem.Qty;
                        foreach (var cItem in consignmentItems)
                        {
                            if (remainingToReverse <= 0) break;

                            var reversible = Math.Min(remainingToReverse, cItem.SoldQty);
                            cItem.SoldQty -= reversible;
                            remainingToReverse -= reversible;
                        }
                    }
                    else
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

            if (idempotencyClaim is { Replayed: false } && _idempotencyService is not null)
                _idempotencyService.Complete(idempotencyClaim, "transaction", transaction.Id);
            await _dbContext.SaveChangesAsync(ct);
            if (_accountingIntegrationService != null)
                await _accountingIntegrationService.EnsureRefundPostedAsync(transaction.Id, request.Id, ct);
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
        if (_capabilityEvaluator is not null)
            await _capabilityEvaluator.EnsureEnabledAsync(CapabilityCodes.SalesPos, request.OutletId, ct);
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

        if (request.Id == Guid.Empty)
            throw new IdempotencyException(IdempotencyErrorCodes.KeyRequired,
                "Checkout request ID wajib tersedia.");
        request = request with
        {
            Payments = request.Payments.Select((payment, index) => payment.Id == Guid.Empty
                ? payment with { Id = CreateDeterministicGuid(request.Id, $"payment:{index}") }
                : payment).ToList()
        };
        if (request.Payments.Select(x => x.Id).Distinct().Count() != request.Payments.Count)
            throw new IdempotencyException(IdempotencyErrorCodes.KeyInvalid,
                "Payment request ID dalam satu checkout tidak boleh duplikat.");
        var requestHash = ComputeRequestHash(request);

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        IdempotencyClaim? idempotencyClaim = null;
        if (_idempotencyService is not null)
            idempotencyClaim = await _idempotencyService.ClaimAsync(
                IdempotencyOperation.Checkout, request.Id, requestHash,
                _currentUserService.UserId.Value, request.OutletId, ct);

        var existing = await _dbContext.Transactions
            .Include(t => t.Outlet)
            .Include(t => t.User)
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .Include(t => t.Payments)
            .FirstOrDefaultAsync(t => t.Id == request.Id, ct);

        if (existing != null)
        {
            await EnsureOutletAccessibleAsync(existing.OutletId, ct);
            if (!string.IsNullOrWhiteSpace(existing.RequestHash) &&
                !string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                throw new IdempotencyException(IdempotencyErrorCodes.KeyReused,
                    "Checkout request ID sudah digunakan untuk payload yang berbeda.");
            if (idempotencyClaim is { Replayed: false } && _idempotencyService is not null)
            {
                _idempotencyService.Complete(idempotencyClaim, "transaction", existing.Id);
                await _dbContext.SaveChangesAsync(ct);
            }
            await dbTx.CommitAsync(ct);
            return await GetByIdAsync(existing.Id, ct);
        }
        if (idempotencyClaim is { Replayed: true })
        {
            await dbTx.CommitAsync(ct);
            return await GetByIdAsync(idempotencyClaim.ResourceId!.Value, ct);
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

        try
        {
            var validatedItems = new List<ValidatedCheckoutItem>();
            Customer? selectedCustomer = null;

            foreach (var itemReq in request.Items)
            {
                var product = await _dbContext.Products
                    .Include(p => p.Recipes)
                    .FirstOrDefaultAsync(p => p.Id == itemReq.ProductId, ct);

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

                // Aggregate stock remains the fast availability check for every tracked mode.
                var stock = product.TrackingMode == ProductTrackingModes.None ? null : await _dbContext.InventoryStocks
                    .FirstOrDefaultAsync(s => s.ProductId == itemReq.ProductId &&
                        s.ProductVariantId == itemReq.ProductVariantId && s.OutletId == request.OutletId, ct);

                if (product.TrackingMode != ProductTrackingModes.None && (stock == null || stock.QtyOnHand < itemReq.Qty))
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

            var totalPayment = request.Payments?.Sum(payment => payment.Amount) ?? 0;
            if (totalPayment != pricingBreakdown.GrandTotal)
            {
                throw new InvalidOperationException($"Total pembayaran ({totalPayment}) harus sama dengan grand total transaksi ({pricingBreakdown.GrandTotal}).");
            }

            var amountPaid = pricingBreakdown.GrandTotal;
            var dueAmount = 0M;
            var status = TransactionStatus.Completed;

            if (request.Payments != null)
            {
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
                Status = status,
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
                AmountPaid = amountPaid,
                DueAmount = dueAmount,
                PaymentDueDate = request.PaymentDueDate,
                RequestHash = requestHash,
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
                Guid transactionItemId;

                if (isConsignment)
                {
                    var consignmentItems = await _dbContext.ConsignmentItems
                        .Include(ci => ci.Consignment)
                        .Where(ci => ci.ProductId == product.Id &&
                                     ci.Consignment.OutletId == request.OutletId &&
                                     ci.Consignment.Status == ConsignmentStatus.Received &&
                                     ci.SoldQty + ci.ReturnedQty < ci.Qty)
                        .OrderBy(ci => ci.Consignment.ReceiveDate) // FIFO
                        .ToListAsync(ct);

                    if (!consignmentItems.Any())
                    {
                        var fallbackItem = await _dbContext.ConsignmentItems
                            .Include(ci => ci.Consignment)
                            .Where(ci => ci.ProductId == product.Id &&
                                         ci.Consignment.OutletId == request.OutletId &&
                                         ci.Consignment.Status == ConsignmentStatus.Received)
                            .OrderByDescending(ci => ci.Consignment.ReceiveDate)
                            .FirstOrDefaultAsync(ct);

                        if (fallbackItem == null)
                        {
                            fallbackItem = await _dbContext.ConsignmentItems
                                .Include(ci => ci.Consignment)
                                .Where(ci => ci.ProductId == product.Id && ci.Consignment.Status == ConsignmentStatus.Received)
                                .OrderByDescending(ci => ci.Consignment.ReceiveDate)
                                .FirstOrDefaultAsync(ct);
                        }

                        if (fallbackItem == null)
                        {
                            throw new InvalidOperationException($"Produk konsinyasi {product.Name} tidak memiliki tanda terima konsinyasi (status: received) yang aktif.");
                        }

                        consignmentItems = new List<ConsignmentItem> { fallbackItem };
                    }

                    itemUnitCost = consignmentItems[0].UnitCost;
                    consignmentSupplierId = consignmentItems[0].Consignment.SupplierId;

                    var item = new TransactionItem
                    {
                        Id = Guid.NewGuid(),
                        TransactionId = newTrx.Id,
                        ProductId = product.Id,
                        ProductVariantId = itemReq.ProductVariantId,
                        Qty = itemReq.Qty,
                        UnitPrice = product.BasePrice,
                        UnitCost = itemUnitCost,
                        DiscountAmount = itemReq.DiscountAmount,
                        LineTotal = (product.BasePrice * itemReq.Qty) - itemReq.DiscountAmount,
                        IsReturned = false,
                        TrackingModeSnapshot = product.TrackingMode
                    };
                    transactionItemId = item.Id;

                    _dbContext.TransactionItems.Add(item);

                    decimal remainingToAllocate = itemReq.Qty;
                    foreach (var cItem in consignmentItems)
                    {
                        if (remainingToAllocate <= 0) break;

                        var remainingAvailable = cItem.Qty - cItem.SoldQty - cItem.ReturnedQty;
                        var allocatable = (cItem == consignmentItems.Last() && remainingAvailable <= 0)
                            ? remainingToAllocate
                            : Math.Min(remainingToAllocate, remainingAvailable);

                        cItem.SoldQty += allocatable;
                        remainingToAllocate -= allocatable;

                        var consignmentSale = new ConsignmentSale
                        {
                            Id = Guid.NewGuid(),
                            SupplierId = cItem.Consignment.SupplierId,
                            TransactionItemId = item.Id,
                            Qty = allocatable,
                            UnitCost = cItem.UnitCost,
                            TotalAmount = allocatable * cItem.UnitCost,
                            Status = ConsignmentSaleStatus.Unpaid,
                            CreatedAt = DateTime.UtcNow
                        };
                        _dbContext.ConsignmentSales.Add(consignmentSale);
                    }
                }
                else
                {
                    var item = new TransactionItem
                    {
                        Id = Guid.NewGuid(),
                        TransactionId = newTrx.Id,
                        ProductId = product.Id,
                        ProductVariantId = itemReq.ProductVariantId,
                        Qty = itemReq.Qty,
                        UnitPrice = product.BasePrice,
                        UnitCost = itemUnitCost,
                        DiscountAmount = itemReq.DiscountAmount,
                        LineTotal = (product.BasePrice * itemReq.Qty) - itemReq.DiscountAmount,
                        IsReturned = false,
                        TrackingModeSnapshot = product.TrackingMode
                    };
                    transactionItemId = item.Id;

                    _dbContext.TransactionItems.Add(item);

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

                await DeductTrackedStockAsync(product, itemReq, request.OutletId,
                    newTrx.Id, transactionItemId, $"Penjualan kasir {trxNumber}", ct);
            }

            if (request.Payments != null)
            {
                foreach (var payReq in request.Payments)
                {
                    var payment = new Payment
                    {
                        Id = payReq.Id,
                        TransactionId = newTrx.Id,
                        CashierSessionId = request.CashierSessionId,
                        Method = payReq.Method,
                        Amount = payReq.Amount,
                        ReferenceNumber = payReq.ReferenceNumber,
                        CreatedAt = DateTime.UtcNow
                    };

                    _dbContext.Payments.Add(payment);
                }
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

            if (idempotencyClaim is { Replayed: false } && _idempotencyService is not null)
                _idempotencyService.Complete(idempotencyClaim, "transaction", newTrx.Id);
            await _dbContext.SaveChangesAsync(ct);
            if (_accountingIntegrationService != null)
            {
                await _accountingIntegrationService.EnsureTransactionPostedAsync(newTrx.Id, ct);
            }
            await dbTx.CommitAsync(ct);

            var stockUpdates = request.Items.Select(i => new StockUpdateItem(i.ProductId, i.Qty)).ToList();
            await _notificationService.SendStockUpdateAsync(request.OutletId, stockUpdates, ct);

            var result = await GetTransactionAggregateAsync(newTrx.Id, ct)
                ?? throw new InvalidOperationException("Transaksi tidak ditemukan setelah checkout.");
            return MapToDto(result, await GetItemAllocationsAsync(result.Id, result.Items.Select(i => i.Id), ct));
        }
        catch
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task DeductTrackedStockAsync(
        Product product,
        CheckoutItemRequest request,
        Guid outletId,
        Guid transactionId,
        Guid transactionItemId,
        string note,
        CancellationToken ct)
    {
        if (product.TrackingMode == ProductTrackingModes.None) return;
        if (product.TrackingMode == ProductTrackingModes.Quantity)
        {
            await _stockService.AddMovementAsync(product.Id, outletId, -request.Qty,
                StockMovementType.Sale, "transaction", transactionId, note, ct,
                request.ProductVariantId, documentItemId: transactionItemId);
            return;
        }

        if (product.TrackingMode == ProductTrackingModes.Batch)
        {
            var requested = request.BatchAllocations?.Where(x => x.Qty > 0).ToList() ?? [];
            var batches = requested.Count > 0
                ? await _dbContext.ProductBatches.Where(x => requested.Select(r => r.ProductBatchId).Contains(x.Id)).ToListAsync(ct)
                : await _dbContext.ProductBatches
                    .Where(x => x.ProductId == product.Id && x.ProductVariantId == request.ProductVariantId &&
                                x.OutletId == outletId && x.QtyRemaining > 0 &&
                                (!x.ExpiryDate.HasValue || x.ExpiryDate.Value.Date >= DateTime.UtcNow.Date))
                    .OrderBy(x => x.ExpiryDate == null).ThenBy(x => x.ExpiryDate).ThenBy(x => x.CreatedAt)
                    .ToListAsync(ct);
            decimal remaining = request.Qty;
            foreach (var batch in batches)
            {
                var wanted = requested.Count > 0
                    ? requested.FirstOrDefault(x => x.ProductBatchId == batch.Id)?.Qty ?? 0
                    : Math.Min(remaining, batch.QtyRemaining);
                if (wanted <= 0) continue;
                if (batch.ProductId != product.Id || batch.ProductVariantId != request.ProductVariantId ||
                    batch.OutletId != outletId || batch.QtyRemaining < wanted ||
                    batch.ExpiryDate?.Date < DateTime.UtcNow.Date)
                    throw new TrackingValidationException(TrackingErrorCodes.BatchStockInsufficient,
                        $"Batch {batch.BatchNumber} tidak tersedia atau stoknya tidak cukup.");
                batch.QtyRemaining -= wanted;
                batch.UpdatedAt = DateTime.UtcNow;
                remaining -= wanted;
                await _stockService.AddMovementAsync(product.Id, outletId, -wanted,
                    StockMovementType.Sale, "transaction", transactionId, note, ct,
                    request.ProductVariantId, batch.Id, documentItemId: transactionItemId);
            }
            if (remaining != 0 || (requested.Count > 0 && requested.Sum(x => x.Qty) != request.Qty))
                throw new TrackingValidationException(TrackingErrorCodes.TrackingAllocationMismatch,
                    "Alokasi batch tidak lengkap atau stok batch tidak mencukupi.");
            return;
        }

        var ids = request.SerialIds?.Distinct().ToList() ?? [];
        if (ids.Count == 0)
            throw new TrackingValidationException(TrackingErrorCodes.SerialRequired,
                $"Serial wajib dipilih untuk produk {product.Name}.");
        if (request.Qty != decimal.Truncate(request.Qty) || ids.Count != request.Qty)
            throw new TrackingValidationException(TrackingErrorCodes.TrackingAllocationMismatch,
                "Jumlah serial harus sama dengan qty dan qty serial harus bilangan bulat.");
        var serials = await _dbContext.ProductSerials.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        if (serials.Count != ids.Count || serials.Any(x => x.ProductId != product.Id ||
            x.ProductVariantId != request.ProductVariantId || x.OutletId != outletId ||
            x.Status != ProductSerialStatuses.Available))
            throw new TrackingValidationException(TrackingErrorCodes.SerialNotAvailable,
                "Satu atau lebih serial tidak tersedia untuk produk dan outlet ini.");
        foreach (var serial in serials)
        {
            serial.Status = ProductSerialStatuses.Sold;
            serial.SoldAt = DateTime.UtcNow;
            serial.UpdatedAt = DateTime.UtcNow;
            await _stockService.AddMovementAsync(product.Id, outletId, -1,
                StockMovementType.Sale, "transaction", transactionId, note, ct,
                request.ProductVariantId, productSerialId: serial.Id, documentItemId: transactionItemId);
        }
    }

    private async Task RestoreTrackedStockAsync(
        TransactionItem item,
        Guid outletId,
        decimal qty,
        Guid transactionId,
        Guid documentItemId,
        string referenceType,
        string note,
        IReadOnlyList<BatchAllocationRequest>? requestedBatches,
        IReadOnlyList<Guid>? requestedSerials,
        Guid operationId,
        CancellationToken ct)
    {
        if (item.TrackingModeSnapshot == ProductTrackingModes.None) return;
        if (item.TrackingModeSnapshot == ProductTrackingModes.Quantity)
        {
            await _stockService.AddMovementAsync(item.ProductId, outletId, qty,
                StockMovementType.Return, referenceType, transactionId, note, ct,
                item.ProductVariantId, documentItemId: documentItemId, operationId: operationId);
            return;
        }

        var netMovements = await GetNetTrackedMovementsAsync(item, transactionId, ct);

        if (item.TrackingModeSnapshot == ProductTrackingModes.Batch)
        {
            var netByBatch = netMovements
                .Where(x => x.ProductBatchId.HasValue)
                .GroupBy(x => x.ProductBatchId!.Value)
                .Select(g => new { BatchId = g.Key, Qty = -g.Sum(x => x.QtyChange), FirstAt = g.Min(x => x.CreatedAt) })
                .Where(x => x.Qty > 0)
                .OrderBy(x => x.FirstAt)
                .ToList();

            var requested = requestedBatches?.Where(x => x.Qty > 0).ToList() ?? [];
            var remainingByBatch = netByBatch.ToDictionary(x => x.BatchId, x => x.Qty);
            if (requested.Count > 0 &&
                (requested.Sum(x => x.Qty) != qty ||
                 requested.Select(x => x.ProductBatchId).Distinct().Count() != requested.Count ||
                 requested.Any(x => !remainingByBatch.TryGetValue(x.ProductBatchId, out var remainingQty) || x.Qty > remainingQty)))
            {
                throw new TrackingValidationException(TrackingErrorCodes.TrackingAllocationMismatch,
                    "Alokasi batch return tidak cocok dengan sisa batch transaksi.");
            }

            var allocations = requested.Count > 0
                ? requested.Select(x => (x.ProductBatchId, x.Qty)).ToList()
                : AllocateRemaining(netByBatch.Select(x => (Id: x.BatchId, x.Qty)), qty);

            foreach (var (batchId, wanted) in allocations)
            {
                var batch = await _dbContext.ProductBatches.SingleOrDefaultAsync(x => x.Id == batchId &&
                    x.ProductId == item.ProductId && x.ProductVariantId == item.ProductVariantId &&
                    x.OutletId == outletId, ct);
                if (batch == null)
                    throw new TrackingValidationException(TrackingErrorCodes.BatchNotFound,
                        "Batch return tidak ditemukan untuk produk dan outlet transaksi.");
                batch.QtyRemaining += wanted;
                batch.UpdatedAt = DateTime.UtcNow;
                await _stockService.AddMovementAsync(item.ProductId, outletId, wanted,
                    StockMovementType.Return, referenceType, transactionId, note, ct,
                    item.ProductVariantId, batch.Id, documentItemId: documentItemId, operationId: operationId);
            }

            return;
        }

        var netBySerial = netMovements
            .Where(x => x.ProductSerialId.HasValue)
            .GroupBy(x => x.ProductSerialId!.Value)
            .Select(g => new { SerialId = g.Key, Qty = -g.Sum(x => x.QtyChange), FirstAt = g.Min(x => x.CreatedAt) })
            .Where(x => x.Qty > 0)
            .OrderBy(x => x.FirstAt)
            .ToList();

        var serialIds = requestedSerials?.Distinct().ToList();
        if (qty != decimal.Truncate(qty))
        {
            throw new TrackingValidationException(TrackingErrorCodes.TrackingAllocationMismatch,
                "Qty serial harus bilangan bulat.");
        }
        if (serialIds == null)
        {
            serialIds = AllocateRemaining(netBySerial.Select(x => (Id: x.SerialId, x.Qty)), qty)
                .Select(x => x.Id).ToList();
        }

        var soldSerialIds = netBySerial.Select(x => x.SerialId).ToHashSet();
        if (serialIds.Count != qty || serialIds.Any(x => !soldSerialIds.Contains(x)))
        {
            throw new TrackingValidationException(TrackingErrorCodes.TrackingAllocationMismatch,
                "Serial return tidak cocok dengan serial transaksi.");
        }
        var serials = await _dbContext.ProductSerials.Where(x => serialIds.Contains(x.Id)).ToListAsync(ct);
        if (serials.Any(x => x.Status != ProductSerialStatuses.Sold))
        {
            throw new TrackingValidationException(TrackingErrorCodes.SerialNotAvailable,
                "Satu atau lebih serial sudah dikembalikan atau tidak valid.");
        }
        foreach (var serial in serials)
        {
            serial.Status = ProductSerialStatuses.Available;
            serial.SoldAt = null;
            serial.UpdatedAt = DateTime.UtcNow;
            await _stockService.AddMovementAsync(item.ProductId, outletId, 1,
                StockMovementType.Return, referenceType, transactionId, note, ct,
                item.ProductVariantId, productSerialId: serial.Id, documentItemId: documentItemId,
                operationId: operationId);
        }
    }

    private async Task<List<StockLedger>> GetNetTrackedMovementsAsync(
        TransactionItem item, Guid transactionId, CancellationToken ct)
    {
        // Old refunds linked restore rows to the Return id; new ones use the transaction item id.
        var returnIds = await _dbContext.Returns.AsNoTracking()
            .Where(x => x.TransactionItemId == item.Id)
            .Select(x => x.Id)
            .ToListAsync(ct);
        var documentItemIds = returnIds.Append(item.Id).ToHashSet();

        return await _dbContext.StockLedgers.AsNoTracking()
            .Where(x => x.ReferenceId == transactionId &&
                        x.DocumentItemId.HasValue && documentItemIds.Contains(x.DocumentItemId.Value) &&
                        (x.ReferenceType == "transaction" || x.ReferenceType == "transaction_refund" ||
                         x.ReferenceType == "transaction_void"))
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    private static List<(Guid Id, decimal Qty)> AllocateRemaining(
        IEnumerable<(Guid Id, decimal Qty)> available, decimal qty)
    {
        var allocations = new List<(Guid Id, decimal Qty)>();
        var remaining = qty;
        foreach (var (id, availableQty) in available)
        {
            if (remaining <= 0) break;
            var wanted = Math.Min(remaining, availableQty);
            if (wanted <= 0) continue;
            allocations.Add((id, wanted));
            remaining -= wanted;
        }
        if (remaining != 0)
        {
            throw new InvalidOperationException("Alokasi tracking tidak mencukupi untuk qty return.");
        }
        return allocations;
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

    private sealed record TransactionItemAllocations(
        Dictionary<Guid, List<TransactionBatchAllocationDto>> Batches,
        Dictionary<Guid, List<TransactionSerialAllocationDto>> Serials);

    private async Task<TransactionItemAllocations> GetItemAllocationsAsync(
        Guid transactionId, IEnumerable<Guid> itemIds, CancellationToken ct)
    {
        var ids = itemIds.ToHashSet();
        var returnItems = await _dbContext.Returns.AsNoTracking()
            .Where(x => x.TransactionId == transactionId && ids.Contains(x.TransactionItemId))
            .Select(x => new { x.Id, x.TransactionItemId })
            .ToListAsync(ct);
        var itemIdByDocumentId = returnItems.ToDictionary(x => x.Id, x => x.TransactionItemId);
        foreach (var id in ids) itemIdByDocumentId[id] = id;
        var documentIds = itemIdByDocumentId.Keys.ToHashSet();
        var movements = await _dbContext.StockLedgers.AsNoTracking()
            .Where(x => x.ReferenceId == transactionId &&
                        (x.ReferenceType == "transaction" || x.ReferenceType == "transaction_refund" ||
                         x.ReferenceType == "transaction_void") &&
                        x.DocumentItemId.HasValue && documentIds.Contains(x.DocumentItemId.Value) &&
                        (x.ProductBatchId != null || x.ProductSerialId != null))
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

        var batchIds = movements.Where(x => x.ProductBatchId.HasValue)
            .Select(x => x.ProductBatchId!.Value).Distinct().ToList();
        var serialIds = movements.Where(x => x.ProductSerialId.HasValue)
            .Select(x => x.ProductSerialId!.Value).Distinct().ToList();

        var batchNumbers = await _dbContext.ProductBatches.AsNoTracking()
            .Where(x => batchIds.Contains(x.Id))
            .Select(x => new { x.Id, x.BatchNumber })
            .ToDictionaryAsync(x => x.Id, x => x.BatchNumber, ct);
        var serialNumbers = await _dbContext.ProductSerials.AsNoTracking()
            .Where(x => serialIds.Contains(x.Id))
            .Select(x => new { x.Id, x.SerialNumber })
            .ToDictionaryAsync(x => x.Id, x => x.SerialNumber, ct);

        var batches = new Dictionary<Guid, List<TransactionBatchAllocationDto>>();
        var serials = new Dictionary<Guid, List<TransactionSerialAllocationDto>>();

        foreach (var movement in movements)
        {
            if (!movement.DocumentItemId.HasValue ||
                !itemIdByDocumentId.TryGetValue(movement.DocumentItemId.Value, out var itemId)) continue;

            if (movement.ReferenceType == "transaction" && movement.QtyChange < 0 &&
                movement.ProductBatchId is { } batchId && batchNumbers.TryGetValue(batchId, out var batchNumber))
            {
                if (!batches.TryGetValue(itemId, out var list))
                    batches[itemId] = list = [];
                var existing = list.Find(x => x.ProductBatchId == batchId);
                if (existing is null)
                    list.Add(new TransactionBatchAllocationDto(batchId, batchNumber, -movement.QtyChange, -movement.QtyChange));
                else
                    list[list.IndexOf(existing)] = existing with
                    {
                        Qty = existing.Qty - movement.QtyChange,
                        RemainingQty = existing.RemainingQty - movement.QtyChange
                    };
            }

            if (movement.ReferenceType == "transaction" && movement.QtyChange < 0 &&
                movement.ProductSerialId is { } serialId && serialNumbers.TryGetValue(serialId, out var serialNumber))
            {
                if (!serials.TryGetValue(itemId, out var list))
                    serials[itemId] = list = [];
                list.Add(new TransactionSerialAllocationDto(serialId, serialNumber, false));
            }
        }

        foreach (var movement in movements.Where(x => x.QtyChange > 0))
        {
            if (!movement.DocumentItemId.HasValue ||
                !itemIdByDocumentId.TryGetValue(movement.DocumentItemId.Value, out var itemId)) continue;

            if (movement.ProductBatchId is { } batchId && batches.TryGetValue(itemId, out var batchList))
            {
                var allocation = batchList.Find(x => x.ProductBatchId == batchId);
                if (allocation is not null)
                    batchList[batchList.IndexOf(allocation)] = allocation with
                    {
                        RemainingQty = Math.Max(0, allocation.RemainingQty - movement.QtyChange)
                    };
            }

            if (movement.ProductSerialId is { } serialId && serials.TryGetValue(itemId, out var serialList))
            {
                var allocation = serialList.Find(x => x.ProductSerialId == serialId);
                if (allocation is not null)
                    serialList[serialList.IndexOf(allocation)] = allocation with { IsReturned = true };
            }
        }

        return new TransactionItemAllocations(batches, serials);
    }

    private static TransactionDto MapToDto(Transaction t, TransactionItemAllocations? allocations = null)
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
            t.GrandTotal,
            0M,
            null,
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
                )
                {
                    ProductVariantId = i.ProductVariantId
                }).ToList()
            ),
            t.Items.Select(i => new TransactionItemDto(
                i.Id,
                i.ProductId,
                i.Product?.Name ?? string.Empty,
                i.ProductVariant?.Sku ?? i.Product?.Sku ?? string.Empty,
                i.Qty,
                returnedQtyByItemId.GetValueOrDefault(i.Id),
                i.Qty - returnedQtyByItemId.GetValueOrDefault(i.Id),
                i.UnitPrice,
                i.UnitCost,
                i.DiscountAmount,
                i.LineTotal
            )
            {
                ProductVariantId = i.ProductVariantId,
                TrackingMode = i.TrackingModeSnapshot,
                Batches = allocations?.Batches.GetValueOrDefault(i.Id) ?? [],
                Serials = allocations?.Serials.GetValueOrDefault(i.Id) ?? []
            }).ToList(),
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
                )
                {
                    ProductVariantId = itemReturn.TransactionItem.ProductVariantId
                })
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
            .Include(t => t.Items).ThenInclude(i => i.ProductVariant)
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

    private string ComputeRequestHash<T>(T request)
        => _idempotencyService?.ComputeRequestHash(request)
           ?? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
               System.Text.Json.JsonSerializer.Serialize(request)))).ToLowerInvariant();

    private static Guid CreateDeterministicGuid(Guid namespaceId, string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{namespaceId:N}:{value}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }
}

file sealed record ValidatedCheckoutItem(Product Product, CheckoutItemRequest Request);
