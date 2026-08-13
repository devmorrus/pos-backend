using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class AccountingIntegrationService : IAccountingIntegrationService
{
    private const string TransactionSaleReference = "transaction_sale";
    private const string PurchaseOrderReference = "purchase_order";
    private const string SupplierPaymentReference = "supplier_payment";
    private const string SupplierReturnReference = "supplier_return";
    private const string ChannelSettlementReference = "channel_settlement";
    private const string ConsignmentSettlementReference = "consignment_settlement";

    private static readonly string[] CashKeywords = ["kas", "cash", "petty"];
    private static readonly string[] BankKeywords = ["bank", "transfer", "giro"];
    private static readonly string[] InventoryKeywords = ["persediaan", "inventory", "stok", "stock"];
    private static readonly string[] AccountsPayableKeywords = ["utang", "hutang", "payable", "supplier"];
    private static readonly string[] ChannelClearingKeywords = ["piutang", "clearing", "channel", "settlement"];
    private static readonly string[] ChannelFeeKeywords = ["fee", "komisi", "commission", "admin", "channel"];
    private static readonly string[] ConsignmentPayableKeywords = ["konsinyasi", "consignment", "titipan"];

    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public AccountingIntegrationService(AppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
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
            .Sum(item => item.UnitCost * item.Qty);
        var consignmentCogs = consignmentSales.Sum(sale => sale.TotalAmount);
        var totalCogs = regularCogs + consignmentCogs;

        var debitAccount = trx.Channel == TransactionChannel.Pos
            ? await ResolveCashBankAccountAsync(businessId, trx.OutletId, preferBank: false, ct)
            : await ResolveChannelClearingAccountAsync(businessId, trx.OutletId, trx.Channel, ct);
        var revenueAccount = await ResolveAccountByTypeAsync(businessId, trx.OutletId, ChartOfAccountType.Revenue, [], false, ct);

        var lines = new List<JournalLine>
        {
            new(debitAccount, trx.GrandTotal, 0),
            new(revenueAccount, 0, trx.GrandTotal),
        };

        if (totalCogs > 0)
        {
            var cogsAccount = await ResolveAccountByTypeAsync(businessId, trx.OutletId, ChartOfAccountType.Cogs, [], false, ct);
            lines.Add(new JournalLine(cogsAccount, totalCogs, 0));

            if (regularCogs > 0)
            {
                var inventoryAccount = await ResolveAccountByTypeAsync(businessId, trx.OutletId, ChartOfAccountType.Asset, InventoryKeywords, false, ct);
                lines.Add(new JournalLine(inventoryAccount, 0, regularCogs));
            }

            if (consignmentCogs > 0)
            {
                var consignmentPayableAccount = await ResolveAccountByTypeAsync(businessId, trx.OutletId, ChartOfAccountType.Liability, ConsignmentPayableKeywords, false, ct);
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

        var businessId = GetBusinessId(purchaseOrder.Outlet.BusinessId);
        var inventoryAccount = await ResolveAccountByTypeAsync(businessId, purchaseOrder.OutletId, ChartOfAccountType.Asset, InventoryKeywords, false, ct);
        ChartOfAccount creditAccount = purchaseOrder.PaymentType switch
        {
            PurchaseOrderPaymentType.Cash => await ResolveCashBankAccountAsync(businessId, purchaseOrder.OutletId, preferBank: false, ct),
            PurchaseOrderPaymentType.Tempo => await ResolveAccountByTypeAsync(businessId, purchaseOrder.OutletId, ChartOfAccountType.Liability, AccountsPayableKeywords, false, ct),
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
        var payableAccount = await ResolveAccountByTypeAsync(businessId, payment.PurchaseOrder.OutletId, ChartOfAccountType.Liability, AccountsPayableKeywords, false, ct);
        var cashBankAccount = await ResolveCashBankAccountAsync(
            businessId,
            payment.PurchaseOrder.OutletId,
            preferBank: !string.Equals(payment.PaymentMethod, PaymentMethod.Cash, StringComparison.OrdinalIgnoreCase),
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
        var payableAccount = await ResolveAccountByTypeAsync(businessId, supplierReturn.PurchaseOrder.OutletId, ChartOfAccountType.Liability, AccountsPayableKeywords, false, ct);
        var inventoryAccount = await ResolveAccountByTypeAsync(businessId, supplierReturn.PurchaseOrder.OutletId, ChartOfAccountType.Asset, InventoryKeywords, false, ct);

        await CreatePostingAsync(
            businessId,
            supplierReturn.PurchaseOrder.OutletId,
            supplierReturn.ReturnDate,
            supplierReturn.ReturnNumber,
            SupplierReturnReference,
            supplierReturn.Id,
            $"Retur supplier {supplierReturn.ReturnNumber}",
            [
                new JournalLine(payableAccount, supplierReturn.TotalAmount, 0),
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
        var cashBankAccount = await ResolveCashBankAccountAsync(businessId, settlement.ChannelAccount.OutletId, preferBank: true, ct);
        var clearingAccount = await ResolveChannelClearingAccountAsync(businessId, settlement.ChannelAccount.OutletId, settlement.ChannelAccount.ChannelName, ct);

        var lines = new List<JournalLine>
        {
            new(cashBankAccount, settlement.NetAmount, 0),
        };

        if (settlement.CommissionAmount > 0)
        {
            var feeAccount = await ResolveAccountByTypeAsync(businessId, settlement.ChannelAccount.OutletId, ChartOfAccountType.Expense, ChannelFeeKeywords, false, ct);
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
        var consignmentPayableAccount = await ResolveAccountByTypeAsync(businessId, settlement.OutletId, ChartOfAccountType.Liability, ConsignmentPayableKeywords, false, ct);
        var cashBankAccount = await ResolveCashBankAccountAsync(businessId, settlement.OutletId, preferBank: true, ct);

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

    public async Task<AccountingPostingStatusDto> GetPostingStatusAsync(string referenceType, Guid referenceId, CancellationToken ct = default)
    {
        var normalizedReferenceType = NormalizeReferenceType(referenceType);
        var entries = await _dbContext.AccountTransactions
            .AsNoTracking()
            .Where(entry => entry.ReferenceType == normalizedReferenceType && entry.ReferenceId == referenceId)
            .OrderBy(entry => entry.TrxDate)
            .ThenBy(entry => entry.TrxNumber)
            .ToListAsync(ct);

        var firstEntry = entries.FirstOrDefault();
        return new AccountingPostingStatusDto(
            normalizedReferenceType,
            referenceId,
            entries.Count > 0,
            entries.Count,
            firstEntry?.TrxNumber,
            firstEntry?.TrxDate);
    }

    public async Task<AccountingBackfillResultDto> BackfillAsync(AccountingBackfillRequest request, CancellationToken ct = default)
    {
        EnsureBusinessContext();

        var dateFrom = request.DateFrom?.Date;
        var dateTo = request.DateTo?.Date.AddDays(1).AddTicks(-1);

        var result = new AccountingBackfillResultDto(0, 0, 0, 0, 0, 0);

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
                if (await EnsureTransactionPostedAsync(transactionId, ct))
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
                if (await EnsurePurchaseOrderPostedAsync(purchaseOrderId, ct))
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
                if (await EnsureSupplierPaymentPostedAsync(paymentId, ct))
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
                if (await EnsureSupplierReturnPostedAsync(returnId, ct))
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
                if (await EnsureChannelSettlementPostedAsync(settlementId, ct))
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
                if (await EnsureConsignmentSettlementPostedAsync(settlementId, ct))
                {
                    postedCount++;
                }
            }

            result = result with { ConsignmentSettlementsPosted = postedCount };
        }

        return result;
    }

    private async Task<bool> HasPostingAsync(string referenceType, Guid referenceId, CancellationToken ct)
    {
        return await _dbContext.AccountTransactions
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

        var entries = lines
            .Where(line => line.DebitAmount > 0 || line.CreditAmount > 0)
            .Select(line => new AccountTransaction
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                OutletId = outletId,
                TrxDate = DateTime.SpecifyKind(trxDate, DateTimeKind.Utc),
                TrxNumber = trxNumber,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                TrxEntity = AccountingTransactionEntity.Business,
                ChartOfAccountId = line.Account.Id,
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount,
                Note = note,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            })
            .ToList();

        _dbContext.AccountTransactions.AddRange(entries);
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task<ChartOfAccount> ResolveCashBankAccountAsync(Guid businessId, Guid outletId, bool preferBank, CancellationToken ct)
    {
        var keywords = preferBank ? BankKeywords : CashKeywords;
        var account = await ResolveAccountAsync(
            businessId,
            outletId,
            ChartOfAccountType.Asset,
            keywords,
            current => current.IsCashBank,
            ct);

        if (account == null)
        {
            account = await ResolveAccountAsync(
                businessId,
                outletId,
                ChartOfAccountType.Asset,
                [],
                current => current.IsCashBank,
                ct);
        }

        return account ?? throw new InvalidOperationException("Akun kas/bank aktif tidak ditemukan untuk kebutuhan posting akuntansi.");
    }

    private async Task<ChartOfAccount> ResolveChannelClearingAccountAsync(Guid businessId, Guid outletId, string channelName, CancellationToken ct)
    {
        var normalizedChannel = (channelName ?? string.Empty).Trim().ToLowerInvariant();
        var keywords = string.IsNullOrWhiteSpace(normalizedChannel)
            ? ChannelClearingKeywords
            : ChannelClearingKeywords.Concat([normalizedChannel]).ToArray();

        var account = await ResolveAccountAsync(
            businessId,
            outletId,
            ChartOfAccountType.Asset,
            keywords,
            current => !current.IsCashBank,
            ct);

        return account ?? throw new InvalidOperationException(
            $"Akun piutang/clearing channel untuk '{channelName}' tidak ditemukan. Siapkan akun asset non kas/bank dengan nama atau kode yang mengandung channel/clearing/piutang.");
    }

    private async Task<ChartOfAccount> ResolveAccountByTypeAsync(
        Guid businessId,
        Guid outletId,
        string accountType,
        string[] keywords,
        bool requireCashBank,
        CancellationToken ct)
    {
        var account = await ResolveAccountAsync(
            businessId,
            outletId,
            accountType,
            keywords,
            current => requireCashBank ? current.IsCashBank : !current.IsCashBank || current.AccountType != ChartOfAccountType.Asset || current.IsCashBank,
            ct);

        return account ?? throw new InvalidOperationException(
            $"Akun {accountType} aktif tidak ditemukan untuk outlet ini. Pastikan Chart of Accounts yang dibutuhkan sudah dibuat dan aktif.");
    }

    private async Task<ChartOfAccount?> ResolveAccountAsync(
        Guid businessId,
        Guid outletId,
        string accountType,
        IEnumerable<string> keywords,
        Func<ChartOfAccount, bool> extraPredicate,
        CancellationToken ct)
    {
        var candidates = await _dbContext.ChartOfAccounts
            .AsNoTracking()
            .Where(account => account.BusinessId == businessId)
            .Where(account => account.IsActive)
            .Where(account => account.AccountType == accountType)
            .Where(account => account.OutletId == null || account.OutletId == outletId)
            .ToListAsync(ct);

        var filtered = candidates
            .Where(extraPredicate)
            .ToList();

        if (filtered.Count == 0)
        {
            return null;
        }

        var keywordArray = keywords
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(keyword => keyword.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();

        return filtered
            .OrderByDescending(account => account.OutletId == outletId)
            .ThenByDescending(account => GetKeywordScore(account, keywordArray))
            .ThenBy(account => account.AccountCode)
            .ThenBy(account => account.AccountName)
            .First();
    }

    private static int GetKeywordScore(ChartOfAccount account, IReadOnlyCollection<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return 0;
        }

        var searchable = $"{account.AccountCode} {account.AccountName}".ToLowerInvariant();
        return keywords.Count(keyword => searchable.Contains(keyword));
    }

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
