using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class AccountingIntegrationService : IAccountingIntegrationService
{
    private const string TransactionSaleReference = "transaction_sale";
    private const string PurchaseOrderReference = "purchase_order";
    private const string ReceivingNoteReference = "receiving_note";
    private const string SupplierPaymentReference = "supplier_payment";
    private const string SupplierReturnReference = "supplier_return";
    private const string ChannelSettlementReference = "channel_settlement";
    private const string ConsignmentSettlementReference = "consignment_settlement";

    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IJournalPostingService _journalPostingService;
    private readonly IAccountingAccountMappingService _mappingService;

    public AccountingIntegrationService(
        AppDbContext dbContext,
        ICurrentUserService currentUserService,
        IJournalPostingService? journalPostingService = null,
        IAccountingAccountMappingService? mappingService = null)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _journalPostingService = journalPostingService ?? new JournalPostingService(dbContext);
        _mappingService = mappingService ?? new AccountingAccountMappingService(dbContext, currentUserService);
    }

    public async Task<bool> EnsureTransactionPostedAsync(Guid transactionId, CancellationToken ct = default)
    {
        var trx = await _dbContext.Transactions
            .Include(current => current.Outlet)
            .Include(current => current.Items).ThenInclude(item => item.Product)
            .Include(current => current.Payments)
            .FirstOrDefaultAsync(current => current.Id == transactionId, ct);

        if (trx == null)
        {
            throw new InvalidOperationException("Transaksi tidak ditemukan.");
        }

        if (trx.Status != TransactionStatus.Completed)
        {
            return false;
        }

        if (await HasPostingAsync(TransactionSaleReference, trx.Id, ct))
        {
            return false;
        }

        var businessId = GetBusinessId(trx.Outlet.BusinessId);
        var consignmentSales = await _dbContext.ConsignmentSales
            .Where(sale => trx.Items.Select(item => item.Id).Contains(sale.TransactionItemId))
            .ToListAsync(ct);

        var regularCogs = trx.Items
            .Where(item => consignmentSales.All(sale => sale.TransactionItemId != item.Id))
            .Sum(item => Math.Round(item.UnitCost * item.Qty, 2));
        var consignmentCogs = consignmentSales.Sum(sale => Math.Round(sale.TotalAmount, 2));
        var totalCogs = regularCogs + consignmentCogs;

        var lines = new List<JournalLine>();
        if (trx.Channel == TransactionChannel.Pos)
        {
            foreach (var paymentGroup in trx.Payments.GroupBy(x => x.Method.ToLowerInvariant()))
            {
                var role = paymentGroup.Key switch
                {
                    PaymentMethod.Cash => AccountingAccountRole.Cash,
                    PaymentMethod.Qris => AccountingAccountRole.ChannelClearing,
                    PaymentMethod.Transfer or PaymentMethod.Edc => AccountingAccountRole.Bank,
                    _ => AccountingAccountRole.Bank,
                };
                var account = await ResolveRoleAsync(businessId, trx.OutletId, role, ct);
                lines.Add(new JournalLine(account, paymentGroup.Sum(x => x.Amount), 0));
            }
        }
        else
        {
            var clearing = await ResolveRoleAsync(businessId, trx.OutletId, AccountingAccountRole.ChannelClearing, ct);
            lines.Add(new JournalLine(clearing, trx.GrandTotal, 0));
        }

        var receivable = trx.DueAmount;
        if (receivable <= 0) receivable = Math.Max(0, trx.GrandTotal - trx.Payments.Sum(x => x.Amount));
        if (receivable > 0)
        {
            var arAccount = await ResolveRoleAsync(businessId, trx.OutletId, AccountingAccountRole.AccountsReceivable, ct);
            lines.Add(new JournalLine(arAccount, receivable, 0));
        }

        var netRevenue = Math.Round(trx.GrandTotal - trx.TaxTotal - trx.ServiceChargeTotal, 2);
        if (netRevenue > 0)
        {
            var revenueAccount = await ResolveRoleAsync(businessId, trx.OutletId, AccountingAccountRole.SalesRevenue, ct);
            lines.Add(new JournalLine(revenueAccount, 0, netRevenue));
        }
        if (trx.TaxTotal > 0)
        {
            var taxAccount = await ResolveRoleAsync(businessId, trx.OutletId, AccountingAccountRole.TaxPayable, ct);
            lines.Add(new JournalLine(taxAccount, 0, trx.TaxTotal));
        }
        if (trx.ServiceChargeTotal > 0)
        {
            var serviceAccount = await ResolveRoleAsync(businessId, trx.OutletId, AccountingAccountRole.ServiceChargeRevenue, ct);
            lines.Add(new JournalLine(serviceAccount, 0, trx.ServiceChargeTotal));
        }

        if (totalCogs > 0)
        {
            var cogsAccount = await ResolveRoleAsync(businessId, trx.OutletId, AccountingAccountRole.Cogs, ct);
            lines.Add(new JournalLine(cogsAccount, totalCogs, 0));

            if (regularCogs > 0)
            {
                var inventoryAccount = await ResolveRoleAsync(businessId, trx.OutletId, AccountingAccountRole.Inventory, ct);
                lines.Add(new JournalLine(inventoryAccount, 0, regularCogs));
            }

            if (consignmentCogs > 0)
            {
                var consignmentPayableAccount = await ResolveRoleAsync(businessId, trx.OutletId, AccountingAccountRole.ConsignmentPayable, ct);
                lines.Add(new JournalLine(consignmentPayableAccount, 0, consignmentCogs));
            }
        }

        await CreatePostingAsync(
            businessId,
            trx.OutletId,
            trx.CreatedAt,
            trx.TransactionNumber,
            TransactionSaleReference,
            trx.Id,
            $"Posting penjualan {trx.TransactionNumber}",
            lines,
            ct);

        return true;
    }

    public async Task<bool> EnsurePurchaseOrderPostedAsync(Guid purchaseOrderId, CancellationToken ct = default)
    {
        var purchaseOrder = await _dbContext.PurchaseOrders
            .Include(current => current.Outlet)
            .Include(current => current.Items)
            .FirstOrDefaultAsync(current => current.Id == purchaseOrderId, ct);

        if (purchaseOrder == null)
        {
            throw new InvalidOperationException("Purchase order tidak ditemukan.");
        }

        if (purchaseOrder.Status != PurchaseOrderStatus.Completed || purchaseOrder.PaymentType == PurchaseOrderPaymentType.Consignment)
        {
            return false;
        }

        if (await HasPostingAsync(PurchaseOrderReference, purchaseOrder.Id, ct))
        {
            return false;
        }

        // Penerimaan barang dengan receiving note diposting per dokumen penerimaan.
        if (await _dbContext.ReceivingNotes.AnyAsync(x => x.PurchaseOrderId == purchaseOrder.Id, ct))
        {
            return false;
        }

        var businessId = GetBusinessId(purchaseOrder.Outlet.BusinessId);
        var inventoryAccount = await ResolveRoleAsync(businessId, purchaseOrder.OutletId, AccountingAccountRole.Inventory, ct);
        ChartOfAccount creditAccount = purchaseOrder.PaymentType switch
        {
            PurchaseOrderPaymentType.Cash => await ResolveRoleAsync(businessId, purchaseOrder.OutletId, AccountingAccountRole.Cash, ct),
            PurchaseOrderPaymentType.Tempo => await ResolveRoleAsync(businessId, purchaseOrder.OutletId, AccountingAccountRole.AccountsPayable, ct),
            _ => throw new InvalidOperationException("Tipe pembayaran purchase order tidak didukung untuk posting akuntansi."),
        };

        var totalAmount = purchaseOrder.Items.Sum(item => item.TotalCost);
        await CreatePostingAsync(
            businessId,
            purchaseOrder.OutletId,
            purchaseOrder.PoDate,
            purchaseOrder.PoNumber,
            PurchaseOrderReference,
            purchaseOrder.Id,
            $"Posting purchase order {purchaseOrder.PoNumber}",
            [
                new JournalLine(inventoryAccount, totalAmount, 0),
                new JournalLine(creditAccount, 0, totalAmount),
            ],
            ct);

        return true;
    }

    public async Task<bool> EnsureReceivingNotePostedAsync(Guid receivingNoteId, CancellationToken ct = default)
    {
        if (await HasPostingAsync(ReceivingNoteReference, receivingNoteId, ct))
        {
            return false;
        }

        var note = await _dbContext.ReceivingNotes
            .Include(x => x.PurchaseOrder).ThenInclude(po => po.Outlet)
            .Include(x => x.PurchaseOrder).ThenInclude(po => po.Items)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == receivingNoteId, ct)
            ?? throw new InvalidOperationException("Receiving note tidak ditemukan.");

        if (note.PurchaseOrder.PaymentType == PurchaseOrderPaymentType.Consignment)
        {
            return false;
        }

        var amount = note.Items.Sum(item =>
        {
            var poItem = note.PurchaseOrder.Items.FirstOrDefault(poi =>
                poi.ProductId == item.ProductId && poi.ProductVariantId == item.ProductVariantId);
            return poItem is null ? 0 : Math.Round(poItem.UnitCost * item.QtyReceived, 2);
        });
        if (amount <= 0) return false;

        var businessId = GetBusinessId(note.PurchaseOrder.Outlet.BusinessId);
        var inventoryAccount = await ResolveRoleAsync(businessId, note.PurchaseOrder.OutletId, AccountingAccountRole.Inventory, ct);
        var creditAccount = note.PurchaseOrder.PaymentType == PurchaseOrderPaymentType.Cash
            ? await ResolveRoleAsync(businessId, note.PurchaseOrder.OutletId, AccountingAccountRole.Cash, ct)
            : await ResolveRoleAsync(businessId, note.PurchaseOrder.OutletId, AccountingAccountRole.AccountsPayable, ct);

        await CreatePostingAsync(
            businessId,
            note.PurchaseOrder.OutletId,
            note.ReceivedDate,
            note.ReceivingNumber,
            ReceivingNoteReference,
            note.Id,
            $"Penerimaan barang {note.ReceivingNumber}",
            [
                new JournalLine(inventoryAccount, amount, 0),
                new JournalLine(creditAccount, 0, amount),
            ],
            ct);

        return true;
    }

    public async Task<bool> EnsureSupplierPaymentPostedAsync(Guid supplierPaymentId, CancellationToken ct = default)
    {
        var payment = await _dbContext.SupplierPayments
            .Include(current => current.PurchaseOrder).ThenInclude(po => po.Outlet)
            .FirstOrDefaultAsync(current => current.Id == supplierPaymentId, ct);

        if (payment == null)
        {
            throw new InvalidOperationException("Pembayaran supplier tidak ditemukan.");
        }

        if (payment.Status != SupplierPaymentStatus.Paid)
        {
            return false;
        }

        if (await HasPostingAsync(SupplierPaymentReference, payment.Id, ct))
        {
            return false;
        }

        var businessId = GetBusinessId(payment.PurchaseOrder.Outlet.BusinessId);
        var payableAccount = await ResolveRoleAsync(businessId, payment.PurchaseOrder.OutletId, AccountingAccountRole.AccountsPayable, ct);
        var cashBankAccount = await ResolveRoleAsync(
            businessId,
            payment.PurchaseOrder.OutletId,
            string.Equals(payment.PaymentMethod, PaymentMethod.Cash, StringComparison.OrdinalIgnoreCase)
                ? AccountingAccountRole.Cash
                : AccountingAccountRole.Bank,
            ct);

        await CreatePostingAsync(
            businessId,
            payment.PurchaseOrder.OutletId,
            payment.PaymentDate,
            payment.ReferenceNumber ?? $"PAY-{payment.Id:N}".ToUpperInvariant(),
            SupplierPaymentReference,
            payment.Id,
            $"Pembayaran supplier untuk PO {payment.PurchaseOrder.PoNumber}",
            [
                new JournalLine(payableAccount, payment.Amount, 0),
                new JournalLine(cashBankAccount, 0, payment.Amount),
            ],
            ct);

        return true;
    }

    public async Task<bool> EnsureSupplierReturnPostedAsync(Guid supplierReturnId, CancellationToken ct = default)
    {
        var supplierReturn = await _dbContext.SupplierReturns
            .Include(current => current.PurchaseOrder).ThenInclude(po => po.Outlet)
            .FirstOrDefaultAsync(current => current.Id == supplierReturnId, ct);

        if (supplierReturn == null)
        {
            throw new InvalidOperationException("Retur supplier tidak ditemukan.");
        }

        if (supplierReturn.Status is not SupplierReturnStatus.Sent and not SupplierReturnStatus.Completed)
        {
            return false;
        }

        if (await HasPostingAsync(SupplierReturnReference, supplierReturn.Id, ct))
        {
            return false;
        }

        var businessId = GetBusinessId(supplierReturn.PurchaseOrder.Outlet.BusinessId);
        var inventoryAccount = await ResolveRoleAsync(businessId, supplierReturn.PurchaseOrder.OutletId, AccountingAccountRole.Inventory, ct);
        var debitAccount = supplierReturn.SettlementTreatment switch
        {
            SupplierReturnSettlement.Receivable => await ResolveRoleAsync(businessId, supplierReturn.PurchaseOrder.OutletId, AccountingAccountRole.AccountsReceivable, ct),
            SupplierReturnSettlement.CashRefund => await ResolveRoleAsync(businessId, supplierReturn.PurchaseOrder.OutletId, AccountingAccountRole.Cash, ct),
            _ => await ResolveRoleAsync(businessId, supplierReturn.PurchaseOrder.OutletId, AccountingAccountRole.AccountsPayable, ct),
        };

        await CreatePostingAsync(
            businessId,
            supplierReturn.PurchaseOrder.OutletId,
            supplierReturn.ReturnDate,
            supplierReturn.ReturnNumber,
            SupplierReturnReference,
            supplierReturn.Id,
            $"Retur supplier {supplierReturn.ReturnNumber}",
            [
                new JournalLine(debitAccount, supplierReturn.TotalAmount, 0),
                new JournalLine(inventoryAccount, 0, supplierReturn.TotalAmount),
            ],
            ct);

        return true;
    }

    public async Task<bool> EnsureChannelSettlementPostedAsync(Guid channelSettlementId, CancellationToken ct = default)
    {
        var settlement = await _dbContext.ChannelSettlements
            .Include(current => current.ChannelAccount).ThenInclude(account => account.Outlet)
            .FirstOrDefaultAsync(current => current.Id == channelSettlementId, ct);

        if (settlement == null)
        {
            throw new InvalidOperationException("Settlement channel tidak ditemukan.");
        }

        if (settlement.Status != ChannelSettlementStatus.Settled)
        {
            return false;
        }

        if (await HasPostingAsync(ChannelSettlementReference, settlement.Id, ct))
        {
            return false;
        }

        var businessId = GetBusinessId(settlement.ChannelAccount.Outlet.BusinessId);
        var cashBankAccount = await ResolveRoleAsync(businessId, settlement.ChannelAccount.OutletId, AccountingAccountRole.Bank, ct);
        var clearingAccount = await ResolveRoleAsync(businessId, settlement.ChannelAccount.OutletId, AccountingAccountRole.ChannelClearing, ct);

        var lines = new List<JournalLine>
        {
            new(cashBankAccount, settlement.NetAmount, 0),
        };

        if (settlement.CommissionAmount > 0)
        {
            var feeAccount = await ResolveRoleAsync(businessId, settlement.ChannelAccount.OutletId, AccountingAccountRole.ChannelFeeExpense, ct);
            lines.Add(new JournalLine(feeAccount, settlement.CommissionAmount, 0));
        }

        lines.Add(new JournalLine(clearingAccount, 0, settlement.GrossAmount));

        await CreatePostingAsync(
            businessId,
            settlement.ChannelAccount.OutletId,
            settlement.SettlementDate,
            settlement.SettlementNumber,
            ChannelSettlementReference,
            settlement.Id,
            $"Settlement channel {settlement.SettlementNumber}",
            lines,
            ct);

        return true;
    }

    public async Task<bool> EnsureConsignmentSettlementPostedAsync(Guid consignmentSettlementId, CancellationToken ct = default)
    {
        var settlement = await _dbContext.ConsignmentSettlements
            .Include(current => current.Outlet)
            .FirstOrDefaultAsync(current => current.Id == consignmentSettlementId, ct);

        if (settlement == null)
        {
            throw new InvalidOperationException("Settlement konsinyasi tidak ditemukan.");
        }

        if (settlement.Status != ConsignmentSettlementStatus.Settled)
        {
            return false;
        }

        if (await HasPostingAsync(ConsignmentSettlementReference, settlement.Id, ct))
        {
            return false;
        }

        var businessId = GetBusinessId(settlement.Outlet.BusinessId);
        var consignmentPayableAccount = await ResolveRoleAsync(businessId, settlement.OutletId, AccountingAccountRole.ConsignmentPayable, ct);
        var cashBankAccount = await ResolveRoleAsync(businessId, settlement.OutletId, AccountingAccountRole.Bank, ct);

        await CreatePostingAsync(
            businessId,
            settlement.OutletId,
            settlement.SettlementDate,
            settlement.SettlementNumber,
            ConsignmentSettlementReference,
            settlement.Id,
            $"Settlement konsinyasi {settlement.SettlementNumber}",
            [
                new JournalLine(consignmentPayableAccount, settlement.TotalAmount, 0),
                new JournalLine(cashBankAccount, 0, settlement.TotalAmount),
            ],
            ct);

        return true;
    }

    public async Task<bool> EnsureRefundPostedAsync(
        Guid transactionId, Guid refundRequestId, CancellationToken ct = default)
    {
        if (await HasPostingAsync("transaction_refund", refundRequestId, ct)) return false;
        var trx = await _dbContext.Transactions
            .Include(x => x.Outlet)
            .Include(x => x.Payments)
            .Include(x => x.Items)
            .Include(x => x.Returns.Where(r => r.RefundRequestId == refundRequestId))
                .ThenInclude(x => x.TransactionItem).ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == transactionId, ct)
            ?? throw new InvalidOperationException("Transaksi refund tidak ditemukan.");

        var returns = trx.Returns.Where(x => x.RefundRequestId == refundRequestId).ToList();
        if (returns.Count == 0) return false;
        EnsureRefundSnapshots(trx, returns);

        var refundAmount = returns.Sum(x => x.RefundAmount);
        var revenueAmount = returns.Sum(x => x.RevenueAmount);
        var taxAmount = returns.Sum(x => x.TaxAmount);
        var serviceAmount = returns.Sum(x => x.ServiceChargeAmount);
        var cogsAmount = returns.Sum(x => x.CogsAmount);
        var consignmentCogsAmount = returns.Sum(x => x.ConsignmentCogsAmount);
        if (refundAmount <= 0) return false;

        var businessId = GetBusinessId(trx.Outlet.BusinessId);
        var lines = new List<JournalLine>();
        if (revenueAmount > 0)
        {
            var salesReturn = await ResolveRoleAsync(businessId, trx.OutletId, AccountingAccountRole.SalesReturns, ct);
            lines.Add(new JournalLine(salesReturn, revenueAmount, 0));
        }
        if (taxAmount > 0)
        {
            var taxAccount = await ResolveRoleAsync(businessId, trx.OutletId, AccountingAccountRole.TaxPayable, ct);
            lines.Add(new JournalLine(taxAccount, taxAmount, 0));
        }
        if (serviceAmount > 0)
        {
            var serviceAccount = await ResolveRoleAsync(businessId, trx.OutletId, AccountingAccountRole.ServiceChargeRevenue, ct);
            lines.Add(new JournalLine(serviceAccount, serviceAmount, 0));
        }

        if (returns.All(x => x.RefundMethod == "exchange"))
        {
            var exchange = await ResolveRoleAsync(businessId, trx.OutletId, AccountingAccountRole.ExchangeClearing, ct);
            lines.Add(new JournalLine(exchange, 0, refundAmount));
        }
        else
        {
            lines.AddRange(await BuildRefundCreditLinesAsync(businessId, trx, refundAmount, ct));
        }

        if (cogsAmount > 0)
        {
            var inventory = await ResolveRoleAsync(businessId, trx.OutletId, AccountingAccountRole.Inventory, ct);
            lines.Add(new JournalLine(inventory, cogsAmount, 0));
        }
        if (consignmentCogsAmount > 0)
        {
            var consignmentPayable = await ResolveRoleAsync(businessId, trx.OutletId, AccountingAccountRole.ConsignmentPayable, ct);
            lines.Add(new JournalLine(consignmentPayable, consignmentCogsAmount, 0));
        }
        if (cogsAmount + consignmentCogsAmount > 0)
        {
            var cogs = await ResolveRoleAsync(businessId, trx.OutletId, AccountingAccountRole.Cogs, ct);
            lines.Add(new JournalLine(cogs, 0, cogsAmount + consignmentCogsAmount));
        }

        await CreatePostingAsync(businessId, trx.OutletId, DateTime.UtcNow,
            $"REF-{trx.TransactionNumber}", "transaction_refund", refundRequestId,
            $"Refund transaksi {trx.TransactionNumber}", lines, ct);
        return true;
    }

    private static void EnsureRefundSnapshots(Transaction trx, IReadOnlyList<Return> returns)
    {
        var totalNet = trx.Items.Sum(x => x.LineTotal);
        foreach (var itemReturn in returns)
        {
            var transactionItem = itemReturn.TransactionItem;
            if (itemReturn.RevenueAmount == 0 && itemReturn.TaxAmount == 0 &&
                itemReturn.ServiceChargeAmount == 0 && itemReturn.RefundAmount == 0)
            {
                itemReturn.RevenueAmount = transactionItem.Qty <= 0
                    ? 0
                    : Math.Round(transactionItem.LineTotal / transactionItem.Qty * itemReturn.Qty, 2);
                itemReturn.TaxAmount = totalNet <= 0
                    ? 0
                    : Math.Round(trx.TaxTotal * itemReturn.RevenueAmount / totalNet, 2);
                itemReturn.ServiceChargeAmount = totalNet <= 0
                    ? 0
                    : Math.Round(trx.ServiceChargeTotal * itemReturn.RevenueAmount / totalNet, 2);
                if (transactionItem.Product?.IsConsignment == true)
                    itemReturn.ConsignmentCogsAmount = Math.Round(transactionItem.UnitCost * itemReturn.Qty, 2);
                else
                    itemReturn.CogsAmount = Math.Round(transactionItem.UnitCost * itemReturn.Qty, 2);
            }
            itemReturn.RefundAmount = itemReturn.RevenueAmount + itemReturn.TaxAmount + itemReturn.ServiceChargeAmount;
        }

        // Final-line rounding correction supaya total proporsional tepat.
        if (returns.Count == 0) return;
        var returnedNet = returns.Sum(x => x.RevenueAmount);
        var last = returns[^1];
        var expectedTax = totalNet <= 0 ? 0 : Math.Round(trx.TaxTotal * returnedNet / totalNet, 2);
        var expectedService = totalNet <= 0 ? 0 : Math.Round(trx.ServiceChargeTotal * returnedNet / totalNet, 2);
        last.TaxAmount += expectedTax - returns.Sum(x => x.TaxAmount);
        last.ServiceChargeAmount += expectedService - returns.Sum(x => x.ServiceChargeAmount);
        last.RefundAmount = last.RevenueAmount + last.TaxAmount + last.ServiceChargeAmount;
    }

    private async Task<IReadOnlyList<JournalLine>> BuildRefundCreditLinesAsync(
        Guid businessId, Transaction trx, decimal refundAmount, CancellationToken ct)
    {
        var payments = trx.Payments.Where(x => x.Amount > 0).ToList();
        var totalPayments = payments.Sum(x => x.Amount);
        if (payments.Count == 0 || totalPayments <= 0)
        {
            var fallbackRole = trx.DueAmount > 0
                ? AccountingAccountRole.AccountsReceivable
                : AccountingAccountRole.Cash;
            return [new JournalLine(await ResolveRoleAsync(businessId, trx.OutletId, fallbackRole, ct), 0, refundAmount)];
        }

        var groups = payments
            .GroupBy(x => x.Method.ToLowerInvariant())
            .Select(x => new { Method = x.Key, Amount = x.Sum(y => y.Amount) })
            .OrderBy(x => x.Method)
            .ToList();
        var lines = new List<JournalLine>();
        decimal allocated = 0;
        for (var i = 0; i < groups.Count; i++)
        {
            var share = i == groups.Count - 1
                ? refundAmount - allocated
                : Math.Round(refundAmount * groups[i].Amount / totalPayments, 2);
            allocated += share;
            if (share == 0) continue;
            var role = groups[i].Method switch
            {
                PaymentMethod.Cash => AccountingAccountRole.Cash,
                PaymentMethod.Qris => AccountingAccountRole.ChannelClearing,
                _ => AccountingAccountRole.Bank,
            };
            lines.Add(new JournalLine(await ResolveRoleAsync(businessId, trx.OutletId, role, ct), 0, share));
        }
        return lines;
    }

    public async Task<bool> EnsureVoidPostedAsync(Guid transactionId, CancellationToken ct = default)
    {
        if (await HasPostingAsync("transaction_void", transactionId, ct)) return false;
        var trx = await _dbContext.Transactions.Include(x => x.Outlet)
            .SingleOrDefaultAsync(x => x.Id == transactionId, ct)
            ?? throw new InvalidOperationException("Transaksi void tidak ditemukan.");
        var original = await _dbContext.JournalEntries.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ReferenceType == TransactionSaleReference && x.ReferenceId == transactionId, ct);
        if (original is null)
            throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.JournalReversalInvalid,
                "Jurnal penjualan asli tidak ditemukan; void tidak dapat diposting.");
        await _journalPostingService.ReverseAsync(original.Id, "transaction_void", transactionId,
            $"VOID-{trx.TransactionNumber}", $"Void transaksi {trx.TransactionNumber}",
            _currentUserService.UserId, ct);
        return true;
    }

    public async Task<bool> EnsurePettyCashPostedAsync(Guid pettyCashExpenseId, CancellationToken ct = default)
    {
        if (await HasPostingAsync("petty_cash_expense", pettyCashExpenseId, ct)) return false;
        var expense = await _dbContext.PettyCashExpenses.Include(x => x.Outlet)
            .SingleOrDefaultAsync(x => x.Id == pettyCashExpenseId, ct)
            ?? throw new InvalidOperationException("Petty cash expense tidak ditemukan.");
        var businessId = GetBusinessId(expense.Outlet.BusinessId);
        var cash = await ResolveRoleAsync(businessId, expense.OutletId, AccountingAccountRole.Cash, ct);
        var expenseAccount = await ResolveRoleAsync(businessId, expense.OutletId, AccountingAccountRole.PettyCashExpense, ct);
        await CreatePostingAsync(businessId, expense.OutletId, expense.CreatedAt,
            $"PETTY-{expense.Id:N}", "petty_cash_expense", expense.Id, expense.Description,
            [new JournalLine(expenseAccount, expense.Amount, 0), new JournalLine(cash, 0, expense.Amount)], ct);
        return true;
    }

    public async Task<bool> EnsureStockOpnamePostedAsync(Guid stockOpnameId, CancellationToken ct = default)
    {
        if (await HasPostingAsync("stock_opname", stockOpnameId, ct)) return false;
        var opname = await _dbContext.StockOpnames.Include(x => x.StockLocation)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == stockOpnameId, ct)
            ?? throw new InvalidOperationException("Stock opname tidak ditemukan.");
        var valued = opname.Items.Where(x => x.Variance != 0)
            .Select(x => new
            {
                x.Variance,
                Amount = Math.Round(Math.Abs(x.Variance) * (x.UnitCostSnapshot > 0
                    ? x.UnitCostSnapshot
                    : x.Product.CostPrice), 2)
            }).ToList();
        if (valued.Count == 0) return false;
        var businessId = GetBusinessId(opname.StockLocation.BusinessId);
        // Opname gudang tidak punya outlet; mapping akun memakai outlet pertama.
        var outletId = opname.StockLocation.OutletId
            ?? await _dbContext.Outlets.Where(x => x.IsActive)
                .OrderBy(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Business belum memiliki outlet untuk posting opname.");
        var inventory = await ResolveRoleAsync(businessId, outletId, AccountingAccountRole.Inventory, ct);
        var lines = new List<JournalLine>();
        var gain = valued.Where(x => x.Variance > 0).Sum(x => x.Amount);
        var loss = valued.Where(x => x.Variance < 0).Sum(x => x.Amount);
        if (gain > 0)
        {
            var gainAccount = await ResolveRoleAsync(businessId, outletId, AccountingAccountRole.InventoryGain, ct);
            lines.Add(new JournalLine(inventory, gain, 0));
            lines.Add(new JournalLine(gainAccount, 0, gain));
        }
        if (loss > 0)
        {
            var lossAccount = await ResolveRoleAsync(businessId, outletId, AccountingAccountRole.InventoryLoss, ct);
            lines.Add(new JournalLine(lossAccount, loss, 0));
            lines.Add(new JournalLine(inventory, 0, loss));
        }
        await CreatePostingAsync(businessId, outletId, opname.CreatedAt,
            $"OPNAME-{opname.Id:N}", "stock_opname", opname.Id, "Penyesuaian stock opname", lines, ct);
        return true;
    }

    public async Task<bool> EnsureCashFlowPostedAsync(Guid cashFlowId, CancellationToken ct = default)
    {
        if (await HasPostingAsync("cash_flow", cashFlowId, ct)) return false;
        var cashFlow = await _dbContext.CashFlows
            .Include(x => x.FromChartOfAccount)
            .Include(x => x.ToChartOfAccount)
            .SingleOrDefaultAsync(x => x.Id == cashFlowId, ct)
            ?? throw new InvalidOperationException("Cash flow tidak ditemukan.");
        if (cashFlow.FromChartOfAccount is null || cashFlow.ToChartOfAccount is null)
            throw new InvalidOperationException("Akun cash flow tidak lengkap.");

        var debit = cashFlow.TrxType == CashFlowType.In
            ? (cashFlow.FromChartOfAccount.IsCashBank ? cashFlow.FromChartOfAccount : cashFlow.ToChartOfAccount)
            : (cashFlow.FromChartOfAccount.IsCashBank ? cashFlow.ToChartOfAccount : cashFlow.FromChartOfAccount);
        var credit = debit == cashFlow.FromChartOfAccount ? cashFlow.ToChartOfAccount : cashFlow.FromChartOfAccount;
        if (debit.Id == credit.Id)
            throw new InvalidOperationException("Akun cash flow tidak valid.");

        await CreatePostingAsync(
            cashFlow.BusinessId,
            cashFlow.OutletId,
            cashFlow.TrxDate,
            cashFlow.TrxNumber,
            "cash_flow",
            cashFlow.Id,
            string.IsNullOrWhiteSpace(cashFlow.Note) ? $"Cash flow {cashFlow.TrxNumber}" : cashFlow.Note,
            [
                new JournalLine(debit, cashFlow.Amount, 0),
                new JournalLine(credit, 0, cashFlow.Amount),
            ],
            ct);
        return true;
    }

    public async Task<AccountingPostingStatusDto> GetPostingStatusAsync(string referenceType, string referenceIdentifier, CancellationToken ct = default)
    {
        var normalizedReferenceType = NormalizeReferenceType(referenceType);
        Guid referenceId = Guid.Empty;

        if (Guid.TryParse(referenceIdentifier, out var parsedGuid))
        {
            referenceId = parsedGuid;
        }
        else
        {
            var cleanedIdentifier = referenceIdentifier.Trim();
            if (normalizedReferenceType == TransactionSaleReference)
            {
                var transaction = await _dbContext.Transactions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TransactionNumber == cleanedIdentifier, ct);
                if (transaction != null)
                {
                    referenceId = transaction.Id;
                }
            }
            else if (normalizedReferenceType == PurchaseOrderReference)
            {
                var po = await _dbContext.PurchaseOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PoNumber == cleanedIdentifier, ct);
                if (po != null)
                {
                    referenceId = po.Id;
                }
            }
        }

        var entries = await _dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry => entry.ReferenceType == normalizedReferenceType && entry.ReferenceId == referenceId)
            .OrderBy(entry => entry.JournalDate)
            .ThenBy(entry => entry.JournalNumber)
            .ToListAsync(ct);

        var firstEntry = entries.FirstOrDefault();
        return new AccountingPostingStatusDto(
            normalizedReferenceType,
            referenceId,
            entries.Count > 0,
            entries.Count,
            firstEntry?.JournalNumber,
            firstEntry?.JournalDate);
    }

    public async Task<AccountingBackfillResultDto> BackfillAsync(AccountingBackfillRequest request, CancellationToken ct = default)
    {
        EnsureBusinessContext();

        var dateFrom = request.DateFrom.HasValue
            ? DateTime.SpecifyKind(request.DateFrom.Value.Date, DateTimeKind.Utc)
            : (DateTime?)null;
        var dateTo = request.DateTo.HasValue
            ? DateTime.SpecifyKind(request.DateTo.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc)
            : (DateTime?)null;

        var result = new AccountingBackfillResultDto(0, 0, 0, 0, 0, 0);
        var scanned = 0;
        var posted = 0;
        var alreadyPosted = 0;
        var skipped = 0;
        var blockedMapping = 0;
        var blockedSource = 0;
        var failed = 0;

        async Task<bool> PostBackfillItemAsync(
            string referenceType, Guid referenceId, Func<Task<bool>> ensure)
        {
            scanned++;
            switch (await RunBackfillItemAsync(referenceType, referenceId, ensure, ct))
            {
                case BackfillOutcome.Posted: posted++; return true;
                case BackfillOutcome.AlreadyPosted: alreadyPosted++; return false;
                case BackfillOutcome.Skipped: skipped++; return false;
                case BackfillOutcome.BlockedMapping: blockedMapping++; return false;
                case BackfillOutcome.BlockedSource: blockedSource++; return false;
                default: failed++; return false;
            }
        }

        if (request.IncludeTransactions)
        {
            var transactionsQuery = _dbContext.Transactions
                .Where(trx => trx.Status == TransactionStatus.Completed)
                .AsQueryable();

            if (dateFrom.HasValue)
            {
                transactionsQuery = transactionsQuery.Where(trx => trx.CreatedAt >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                transactionsQuery = transactionsQuery.Where(trx => trx.CreatedAt <= dateTo.Value);
            }

            var transactionIds = await transactionsQuery.Select(trx => trx.Id).ToListAsync(ct);
            var postedCount = 0;
            foreach (var transactionId in transactionIds)
            {
                if (await PostBackfillItemAsync(TransactionSaleReference, transactionId,
                        () => EnsureTransactionPostedAsync(transactionId, ct)))
                {
                    postedCount++;
                }
            }

            result = result with { TransactionsPosted = postedCount };
        }

        if (request.IncludePurchaseOrders)
        {
            var purchaseOrdersQuery = _dbContext.PurchaseOrders
                .Where(po => po.Status == PurchaseOrderStatus.Completed && po.PaymentType != PurchaseOrderPaymentType.Consignment)
                .AsQueryable();

            if (dateFrom.HasValue)
            {
                purchaseOrdersQuery = purchaseOrdersQuery.Where(po => po.PoDate >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                purchaseOrdersQuery = purchaseOrdersQuery.Where(po => po.PoDate <= dateTo.Value);
            }

            var purchaseOrderIds = await purchaseOrdersQuery.Select(po => po.Id).ToListAsync(ct);
            var postedCount = 0;
            foreach (var purchaseOrderId in purchaseOrderIds)
            {
                if (await PostBackfillItemAsync(PurchaseOrderReference, purchaseOrderId,
                        () => EnsurePurchaseOrderPostedAsync(purchaseOrderId, ct)))
                {
                    postedCount++;
                }
            }

            result = result with { PurchaseOrdersPosted = postedCount };
        }

        if (request.IncludeSupplierPayments)
        {
            var paymentsQuery = _dbContext.SupplierPayments
                .Where(payment => payment.Status == SupplierPaymentStatus.Paid)
                .AsQueryable();

            if (dateFrom.HasValue)
            {
                paymentsQuery = paymentsQuery.Where(payment => payment.PaymentDate >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                paymentsQuery = paymentsQuery.Where(payment => payment.PaymentDate <= dateTo.Value);
            }

            var paymentIds = await paymentsQuery.Select(payment => payment.Id).ToListAsync(ct);
            var postedCount = 0;
            foreach (var paymentId in paymentIds)
            {
                if (await PostBackfillItemAsync(SupplierPaymentReference, paymentId,
                        () => EnsureSupplierPaymentPostedAsync(paymentId, ct)))
                {
                    postedCount++;
                }
            }

            result = result with { SupplierPaymentsPosted = postedCount };
        }

        if (request.IncludeSupplierReturns)
        {
            var returnsQuery = _dbContext.SupplierReturns
                .Where(current => current.Status == SupplierReturnStatus.Sent || current.Status == SupplierReturnStatus.Completed)
                .AsQueryable();

            if (dateFrom.HasValue)
            {
                returnsQuery = returnsQuery.Where(current => current.ReturnDate >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                returnsQuery = returnsQuery.Where(current => current.ReturnDate <= dateTo.Value);
            }

            var returnIds = await returnsQuery.Select(current => current.Id).ToListAsync(ct);
            var postedCount = 0;
            foreach (var returnId in returnIds)
            {
                if (await PostBackfillItemAsync(SupplierReturnReference, returnId,
                        () => EnsureSupplierReturnPostedAsync(returnId, ct)))
                {
                    postedCount++;
                }
            }

            result = result with { SupplierReturnsPosted = postedCount };
        }

        if (request.IncludeChannelSettlements)
        {
            var settlementsQuery = _dbContext.ChannelSettlements
                .Where(current => current.Status == ChannelSettlementStatus.Settled)
                .AsQueryable();

            if (dateFrom.HasValue)
            {
                settlementsQuery = settlementsQuery.Where(current => current.SettlementDate >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                settlementsQuery = settlementsQuery.Where(current => current.SettlementDate <= dateTo.Value);
            }

            var settlementIds = await settlementsQuery.Select(current => current.Id).ToListAsync(ct);
            var postedCount = 0;
            foreach (var settlementId in settlementIds)
            {
                if (await PostBackfillItemAsync(ChannelSettlementReference, settlementId,
                        () => EnsureChannelSettlementPostedAsync(settlementId, ct)))
                {
                    postedCount++;
                }
            }

            result = result with { ChannelSettlementsPosted = postedCount };
        }

        if (request.IncludeConsignmentSettlements)
        {
            var settlementsQuery = _dbContext.ConsignmentSettlements
                .Where(current => current.Status == ConsignmentSettlementStatus.Settled)
                .AsQueryable();

            if (dateFrom.HasValue)
            {
                settlementsQuery = settlementsQuery.Where(current => current.SettlementDate >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                settlementsQuery = settlementsQuery.Where(current => current.SettlementDate <= dateTo.Value);
            }

            var settlementIds = await settlementsQuery.Select(current => current.Id).ToListAsync(ct);
            var postedCount = 0;
            foreach (var settlementId in settlementIds)
            {
                if (await PostBackfillItemAsync(ConsignmentSettlementReference, settlementId,
                        () => EnsureConsignmentSettlementPostedAsync(settlementId, ct)))
                {
                    postedCount++;
                }
            }

            result = result with { ConsignmentSettlementsPosted = postedCount };
        }

        if (request.IncludeReceivingNotes)
        {
            var query = _dbContext.ReceivingNotes
                .Where(current => current.PurchaseOrder.PaymentType != PurchaseOrderPaymentType.Consignment)
                .AsQueryable();
            if (dateFrom.HasValue) query = query.Where(x => x.ReceivedDate >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(x => x.ReceivedDate <= dateTo.Value);
            var ids = await query.Select(x => x.Id).ToListAsync(ct);
            var postedCount = 0;
            foreach (var id in ids)
                if (await PostBackfillItemAsync(ReceivingNoteReference, id,
                        () => EnsureReceivingNotePostedAsync(id, ct))) postedCount++;
            result = result with { ReceivingNotesPosted = postedCount };
        }

        if (request.IncludeRefunds)
        {
            var query = _dbContext.Returns.AsNoTracking().AsQueryable();
            if (dateFrom.HasValue) query = query.Where(x => x.CreatedAt >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(x => x.CreatedAt <= dateTo.Value);
            var refunds = await query
                .Select(x => new { x.RefundRequestId, x.TransactionId })
                .Distinct()
                .ToListAsync(ct);
            var postedCount = 0;
            foreach (var refund in refunds)
                if (await PostBackfillItemAsync("transaction_refund", refund.RefundRequestId,
                        () => EnsureRefundPostedAsync(refund.TransactionId, refund.RefundRequestId, ct))) postedCount++;
            result = result with { RefundsPosted = postedCount };
        }

        if (request.IncludeVoids)
        {
            var query = _dbContext.Transactions
                .Where(x => x.Status == TransactionStatus.Voided)
                .Where(x => !_dbContext.JournalEntries.Any(j =>
                    j.ReferenceType == "transaction_void" && j.ReferenceId == x.Id))
                .AsQueryable();
            if (dateFrom.HasValue) query = query.Where(x => x.CreatedAt >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(x => x.CreatedAt <= dateTo.Value);
            var ids = await query.Select(x => x.Id).ToListAsync(ct);
            var postedCount = 0;
            foreach (var id in ids)
                if (await PostBackfillItemAsync("transaction_void", id,
                        () => EnsureVoidPostedAsync(id, ct))) postedCount++;
            result = result with { VoidsPosted = postedCount };
        }

        if (request.IncludePettyCash)
        {
            var query = _dbContext.PettyCashExpenses.AsQueryable();
            if (dateFrom.HasValue) query = query.Where(x => x.CreatedAt >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(x => x.CreatedAt <= dateTo.Value);
            var ids = await query.Select(x => x.Id).ToListAsync(ct);
            var postedCount = 0;
            foreach (var id in ids)
                if (await PostBackfillItemAsync("petty_cash_expense", id,
                        () => EnsurePettyCashPostedAsync(id, ct))) postedCount++;
            result = result with { PettyCashPosted = postedCount };
        }

        if (request.IncludeStockOpnames)
        {
            var query = _dbContext.StockOpnames.Where(x => x.Status == "completed").AsQueryable();
            if (dateFrom.HasValue) query = query.Where(x => x.CreatedAt >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(x => x.CreatedAt <= dateTo.Value);
            var ids = await query.Select(x => x.Id).ToListAsync(ct);
            var postedCount = 0;
            foreach (var id in ids)
                if (await PostBackfillItemAsync("stock_opname", id,
                        () => EnsureStockOpnamePostedAsync(id, ct))) postedCount++;
            result = result with { StockOpnamesPosted = postedCount };
        }

        if (request.IncludeCashFlows)
        {
            var query = _dbContext.CashFlows.AsQueryable();
            if (dateFrom.HasValue) query = query.Where(x => x.TrxDate >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(x => x.TrxDate <= dateTo.Value);
            var ids = await query.Select(x => x.Id).ToListAsync(ct);
            var postedCount = 0;
            foreach (var id in ids)
                if (await PostBackfillItemAsync("cash_flow", id,
                        () => EnsureCashFlowPostedAsync(id, ct))) postedCount++;
            result = result with { CashFlowsPosted = postedCount };
        }

        return result with
        {
            Scanned = scanned,
            Posted = posted,
            AlreadyPosted = alreadyPosted,
            SkippedNotApplicable = skipped,
            BlockedMissingMapping = blockedMapping,
            BlockedInvalidSource = blockedSource,
            Failed = failed,
        };
    }

    private async Task<BackfillOutcome> RunBackfillItemAsync(
        string referenceType, Guid referenceId, Func<Task<bool>> ensure, CancellationToken ct)
    {
        if (await HasPostingAsync(referenceType, referenceId, ct))
        {
            return BackfillOutcome.AlreadyPosted;
        }

        try
        {
            return await ensure() ? BackfillOutcome.Posted : BackfillOutcome.Skipped;
        }
        catch (AccountingIntegrityException ex)
            when (ex.Code == AccountingIntegrityErrorCodes.AccountMappingMissing)
        {
            _dbContext.ChangeTracker.Clear();
            return BackfillOutcome.BlockedMapping;
        }
        catch (AccountingIntegrityException)
        {
            _dbContext.ChangeTracker.Clear();
            return BackfillOutcome.BlockedSource;
        }
        catch (InvalidOperationException)
        {
            _dbContext.ChangeTracker.Clear();
            return BackfillOutcome.BlockedSource;
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            return BackfillOutcome.Failed;
        }
    }

    private enum BackfillOutcome
    {
        Posted,
        AlreadyPosted,
        Skipped,
        BlockedMapping,
        BlockedSource,
        Failed,
    }

    private async Task<bool> HasPostingAsync(string referenceType, Guid referenceId, CancellationToken ct)
    {
        return await _dbContext.JournalEntries
            .AsNoTracking()
            .AnyAsync(entry => entry.ReferenceType == referenceType && entry.ReferenceId == referenceId, ct);
    }

    private async Task CreatePostingAsync(
        Guid businessId,
        Guid? outletId,
        DateTime trxDate,
        string trxNumber,
        string referenceType,
        Guid referenceId,
        string note,
        IReadOnlyCollection<JournalLine> lines,
        CancellationToken ct)
    {
        if (lines.Count < 2)
        {
            throw new InvalidOperationException("Posting jurnal minimal harus memiliki dua baris.");
        }

        var totalDebit = lines.Sum(line => line.DebitAmount);
        var totalCredit = lines.Sum(line => line.CreditAmount);
        if (totalDebit <= 0 || totalCredit <= 0 || totalDebit != totalCredit)
        {
            throw new InvalidOperationException("Posting jurnal tidak seimbang.");
        }

        await _journalPostingService.PostAsync(new JournalDraft(
            businessId, outletId, trxDate, trxNumber, referenceType, referenceId, note,
            lines.Where(line => line.DebitAmount > 0 || line.CreditAmount > 0)
                .Select(line => new JournalDraftLine(
                    line.Account.Id, line.DebitAmount, line.CreditAmount, note)).ToList(),
            CreatedBy: _currentUserService.UserId), ct);
    }

    private Task<ChartOfAccount> ResolveRoleAsync(
        Guid businessId, Guid outletId, string role, CancellationToken ct)
        => _mappingService.ResolveAsync(businessId, outletId, role, ct);

    private Guid EnsureBusinessContext()
    {
        if (!_currentUserService.BusinessId.HasValue)
        {
            throw new UnauthorizedAccessException("Business context tidak ditemukan.");
        }

        return _currentUserService.BusinessId.Value;
    }

    private Guid GetBusinessId(Guid? sourceBusinessId)
    {
        var businessId = EnsureBusinessContext();
        if (sourceBusinessId.HasValue && sourceBusinessId.Value != businessId)
        {
            throw new UnauthorizedAccessException("Transaksi berada di luar business context aktif.");
        }

        return businessId;
    }

    private static string NormalizeReferenceType(string referenceType)
    {
        if (string.IsNullOrWhiteSpace(referenceType))
        {
            throw new InvalidOperationException("Reference type wajib diisi.");
        }

        return referenceType.Trim().ToLowerInvariant();
    }

    private sealed record JournalLine(ChartOfAccount Account, decimal DebitAmount, decimal CreditAmount);
}
