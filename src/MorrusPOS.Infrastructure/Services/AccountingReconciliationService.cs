using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class AccountingReconciliationService : IAccountingReconciliationService
{
    private const int MaxPreviewIssues = 500;

    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public AccountingReconciliationService(
        AppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<AccountingReadinessDto> GetReadinessAsync(CancellationToken ct = default)
    {
        var businessId = EnsureBusinessContext();
        var mappings = await _dbContext.AccountingAccountMappings.AsNoTracking()
            .Include(x => x.ChartOfAccount)
            .Where(x => x.BusinessId == businessId)
            .ToListAsync(ct);

        var missing = new List<string>();
        var invalid = new List<AccountingMappingIssueDto>();
        foreach (var role in AccountingAccountRole.All)
        {
            var roleMappings = mappings.Where(x => x.Role == role).ToList();
            if (roleMappings.Count == 0)
            {
                missing.Add(role);
                continue;
            }

            foreach (var mapping in roleMappings)
            {
                var account = mapping.ChartOfAccount;
                var reason = account is null
                    ? "Akun mapping tidak ditemukan."
                    : !account.IsActive || account.BusinessId != businessId
                        ? "Akun mapping tidak aktif atau milik business lain."
                        : mapping.OutletId is null && account.OutletId.HasValue
                            ? "Mapping business memakai akun khusus outlet."
                            : mapping.OutletId.HasValue && account.OutletId.HasValue &&
                              account.OutletId != mapping.OutletId
                                ? "Akun mapping bukan milik outlet mapping."
                                : null;
                if (reason is not null)
                    invalid.Add(new AccountingMappingIssueDto(role, mapping.OutletId, reason));
            }
        }

        var journals = await _dbContext.JournalEntries.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.TotalDebit,
                x.TotalCredit,
                LineCount = x.Lines.Count(),
                LineDebit = x.Lines.Sum(y => y.DebitAmount),
                LineCredit = x.Lines.Sum(y => y.CreditAmount),
            })
            .ToListAsync(ct);
        var unbalanced = journals.Count(x =>
            x.LineCount < 2 || x.TotalDebit != x.TotalCredit ||
            x.LineDebit != x.LineCredit || x.LineDebit != x.TotalDebit || x.LineCredit != x.TotalCredit);

        var unposted = await GetUnpostedCountsAsync(businessId, ct);
        return new AccountingReadinessDto(
            businessId,
            missing.Count == 0 && invalid.Count == 0 && unbalanced == 0,
            AccountingAccountRole.All,
            missing,
            invalid,
            unbalanced,
            unposted);
    }

    public async Task<AccountingReconciliationPreviewDto> PreviewAsync(
        AccountingReconciliationRequest request, CancellationToken ct = default)
    {
        var businessId = EnsureBusinessContext();
        var from = request.DateFrom.HasValue
            ? DateTime.SpecifyKind(request.DateFrom.Value.Date, DateTimeKind.Utc)
            : (DateTime?)null;
        var to = request.DateTo.HasValue
            ? DateTime.SpecifyKind(request.DateTo.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc)
            : (DateTime?)null;

        var journalsQuery = _dbContext.JournalEntries.AsNoTracking()
            .Include(x => x.Lines).ThenInclude(x => x.ChartOfAccount)
            .Where(x => x.BusinessId == businessId);
        if (from.HasValue) journalsQuery = journalsQuery.Where(x => x.JournalDate >= from.Value);
        if (to.HasValue) journalsQuery = journalsQuery.Where(x => x.JournalDate <= to.Value);

        var journals = await journalsQuery.OrderBy(x => x.JournalDate).Take(10000).ToListAsync(ct);
        var issues = new List<AccountingReconciliationIssueDto>();

        void AddIssue(string type, string? referenceType, Guid? referenceId, Guid? journalId,
            string message, bool repairable = false,
            decimal? expectedDebit = null, decimal? expectedCredit = null,
            decimal? actualDebit = null, decimal? actualCredit = null)
        {
            if (issues.Count >= MaxPreviewIssues) return;
            issues.Add(new AccountingReconciliationIssueDto(type, referenceType, referenceId, journalId,
                expectedDebit, expectedCredit, actualDebit, actualCredit, repairable, message));
        }

        foreach (var journal in journals)
        {
            var lineDebit = journal.Lines.Sum(x => x.DebitAmount);
            var lineCredit = journal.Lines.Sum(x => x.CreditAmount);
            if (journal.Lines.Count < 2)
                AddIssue("JOURNAL_TOO_FEW_LINES", journal.ReferenceType, journal.ReferenceId, journal.Id,
                    "Jurnal mempunyai kurang dari dua baris.", actualDebit: lineDebit, actualCredit: lineCredit);
            if (lineDebit != lineCredit || lineDebit != journal.TotalDebit || lineCredit != journal.TotalCredit ||
                journal.TotalDebit != journal.TotalCredit)
                AddIssue("JOURNAL_UNBALANCED", journal.ReferenceType, journal.ReferenceId, journal.Id,
                    "Total debit/kredit header tidak sama dengan baris jurnal.",
                    expectedDebit: journal.TotalDebit, expectedCredit: journal.TotalCredit,
                    actualDebit: lineDebit, actualCredit: lineCredit);

            foreach (var line in journal.Lines)
            {
                if (line.BusinessId != journal.BusinessId)
                    AddIssue("JOURNAL_TENANT_MISMATCH", journal.ReferenceType, journal.ReferenceId, journal.Id,
                        "Baris jurnal memakai business yang berbeda dari header.", actualDebit: line.DebitAmount, actualCredit: line.CreditAmount);
                if (line.OutletId != journal.OutletId)
                    AddIssue("JOURNAL_OUTLET_MISMATCH", journal.ReferenceType, journal.ReferenceId, journal.Id,
                        "Baris jurnal memakai outlet yang berbeda dari header.", actualDebit: line.DebitAmount, actualCredit: line.CreditAmount);
                if (line.ChartOfAccount is null || line.ChartOfAccount.BusinessId != journal.BusinessId)
                    AddIssue("JOURNAL_ACCOUNT_TENANT_MISMATCH", journal.ReferenceType, journal.ReferenceId, journal.Id,
                        "Baris jurnal memakai chart of account tenant lain.", actualDebit: line.DebitAmount, actualCredit: line.CreditAmount);
                else if (line.ChartOfAccount.OutletId.HasValue &&
                         line.ChartOfAccount.OutletId != journal.OutletId)
                    AddIssue("JOURNAL_ACCOUNT_OUTLET_MISMATCH", journal.ReferenceType, journal.ReferenceId, journal.Id,
                        "Baris jurnal memakai account khusus outlet lain.", actualDebit: line.DebitAmount, actualCredit: line.CreditAmount);
            }

            // Hash backfill historis memakai canonical lama dan tidak dapat direkonstruksi.
            if (!journal.Description.StartsWith("Backfill jurnal historis", StringComparison.Ordinal))
            {
                var draft = new JournalDraft(
                    journal.BusinessId, journal.OutletId, journal.JournalDate, journal.JournalNumber,
                    journal.ReferenceType, journal.ReferenceId, journal.Description,
                    journal.Lines.OrderBy(x => x.LineNumber)
                        .Select(x => new JournalDraftLine(x.ChartOfAccountId, x.DebitAmount, x.CreditAmount))
                        .ToList(),
                    journal.ReversalOfJournalId, journal.CreatedBy);
                if (!string.Equals(JournalPostingService.ComputeHash(draft), journal.PostingHash,
                        StringComparison.Ordinal))
                    AddIssue("JOURNAL_HASH_MISMATCH", journal.ReferenceType, journal.ReferenceId, journal.Id,
                        "Posting hash tidak sesuai dengan isi jurnal.");
            }
        }

        var duplicateSources = journals
            .GroupBy(x => new { x.ReferenceType, x.ReferenceId })
            .Where(x => x.Count() > 1);
        foreach (var duplicate in duplicateSources)
            AddIssue("JOURNAL_REFERENCE_DUPLICATE", duplicate.Key.ReferenceType, duplicate.Key.ReferenceId,
                duplicate.First().Id, "Dokumen sumber mempunyai lebih dari satu jurnal.");

        // Reversal pasangan bisa berada di luar rentang tanggal preview.
        var originalIds = journals.Where(x => x.ReversalOfJournalId.HasValue)
            .Select(x => x.ReversalOfJournalId!.Value).Distinct().ToList();
        var existingOriginalIds = originalIds.Count == 0
            ? new List<Guid>()
            : await _dbContext.JournalEntries.AsNoTracking()
                .Where(x => x.BusinessId == businessId && originalIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(ct);
        foreach (var journal in journals.Where(x => x.ReversalOfJournalId.HasValue))
        {
            if (!existingOriginalIds.Contains(journal.ReversalOfJournalId!.Value))
                AddIssue("REVERSAL_ORIGINAL_MISSING", journal.ReferenceType, journal.ReferenceId, journal.Id,
                    "Jurnal reversal tidak menemukan jurnal asli.");
        }
        var reversedIds = journals.Where(x => x.Status == JournalEntryStatus.Reversed)
            .Select(x => x.Id).ToList();
        var reversalPairs = reversedIds.Count == 0
            ? new List<Guid>()
            : await _dbContext.JournalEntries.AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.ReversalOfJournalId.HasValue &&
                    reversedIds.Contains(x.ReversalOfJournalId.Value))
                .Select(x => x.ReversalOfJournalId!.Value)
                .ToListAsync(ct);
        foreach (var journal in journals.Where(x => x.Status == JournalEntryStatus.Reversed))
        {
            if (!reversalPairs.Contains(journal.Id))
                AddIssue("REVERSAL_MISSING", journal.ReferenceType, journal.ReferenceId, journal.Id,
                    "Jurnal ditandai reversed tetapi tidak mempunyai jurnal reversal.");
        }

        await AddMissingSourceIssuesAsync(businessId, from, to, AddIssue, ct);
        return new AccountingReconciliationPreviewDto(issues.Count, issues);
    }

    private async Task AddMissingSourceIssuesAsync(
        Guid businessId,
        DateTime? from,
        DateTime? to,
        Action<string, string?, Guid?, Guid?, string, bool, decimal?, decimal?, decimal?, decimal?> addIssue,
        CancellationToken ct)
    {
        var saleQuery = _dbContext.Transactions
            .Where(x => x.BusinessId == businessId && x.Status == TransactionStatus.Completed)
            .Where(x => !_dbContext.JournalEntries.Any(j =>
                j.BusinessId == businessId && j.ReferenceType == "transaction_sale" && j.ReferenceId == x.Id));
        var voidQuery = _dbContext.Transactions
            .Where(x => x.BusinessId == businessId && x.Status == TransactionStatus.Voided)
            .Where(x => !_dbContext.JournalEntries.Any(j =>
                j.BusinessId == businessId && j.ReferenceType == "transaction_void" && j.ReferenceId == x.Id));
        var receivingQuery = _dbContext.ReceivingNotes
            .Where(x => x.BusinessId == businessId &&
                x.PurchaseOrder.PaymentType != PurchaseOrderPaymentType.Consignment)
            .Where(x => !_dbContext.JournalEntries.Any(j =>
                j.BusinessId == businessId && j.ReferenceType == "receiving_note" && j.ReferenceId == x.Id));
        var paymentQuery = _dbContext.SupplierPayments
            .Where(x => x.BusinessId == businessId && x.Status == SupplierPaymentStatus.Paid)
            .Where(x => !_dbContext.JournalEntries.Any(j =>
                j.BusinessId == businessId && j.ReferenceType == "supplier_payment" && j.ReferenceId == x.Id));
        var supplierReturnQuery = _dbContext.SupplierReturns
            .Where(x => x.BusinessId == businessId &&
                (x.Status == SupplierReturnStatus.Sent || x.Status == SupplierReturnStatus.Completed))
            .Where(x => !_dbContext.JournalEntries.Any(j =>
                j.BusinessId == businessId && j.ReferenceType == "supplier_return" && j.ReferenceId == x.Id));
        var channelQuery = _dbContext.ChannelSettlements
            .Where(x => x.BusinessId == businessId && x.Status == ChannelSettlementStatus.Settled)
            .Where(x => !_dbContext.JournalEntries.Any(j =>
                j.BusinessId == businessId && j.ReferenceType == "channel_settlement" && j.ReferenceId == x.Id));
        var consignmentQuery = _dbContext.ConsignmentSettlements
            .Where(x => x.BusinessId == businessId && x.Status == ConsignmentSettlementStatus.Settled)
            .Where(x => !_dbContext.JournalEntries.Any(j =>
                j.BusinessId == businessId && j.ReferenceType == "consignment_settlement" && j.ReferenceId == x.Id));
        var pettyQuery = _dbContext.PettyCashExpenses
            .Where(x => x.BusinessId == businessId)
            .Where(x => !_dbContext.JournalEntries.Any(j =>
                j.BusinessId == businessId && j.ReferenceType == "petty_cash_expense" && j.ReferenceId == x.Id));
        var opnameQuery = _dbContext.StockOpnames
            .Where(x => x.BusinessId == businessId && x.Status == "completed")
            .Where(x => !_dbContext.JournalEntries.Any(j =>
                j.BusinessId == businessId && j.ReferenceType == "stock_opname" && j.ReferenceId == x.Id));
        var cashFlowQuery = _dbContext.CashFlows
            .Where(x => x.BusinessId == businessId)
            .Where(x => !_dbContext.JournalEntries.Any(j =>
                j.BusinessId == businessId && j.ReferenceType == "cash_flow" && j.ReferenceId == x.Id));

        if (from.HasValue)
        {
            saleQuery = saleQuery.Where(x => x.CreatedAt >= from.Value);
            voidQuery = voidQuery.Where(x => x.CreatedAt >= from.Value);
            receivingQuery = receivingQuery.Where(x => x.ReceivedDate >= from.Value);
            paymentQuery = paymentQuery.Where(x => x.PaymentDate >= from.Value);
            supplierReturnQuery = supplierReturnQuery.Where(x => x.ReturnDate >= from.Value);
            channelQuery = channelQuery.Where(x => x.SettlementDate >= from.Value);
            consignmentQuery = consignmentQuery.Where(x => x.SettlementDate >= from.Value);
            pettyQuery = pettyQuery.Where(x => x.CreatedAt >= from.Value);
            opnameQuery = opnameQuery.Where(x => x.CreatedAt >= from.Value);
            cashFlowQuery = cashFlowQuery.Where(x => x.TrxDate >= from.Value);
        }
        if (to.HasValue)
        {
            saleQuery = saleQuery.Where(x => x.CreatedAt <= to.Value);
            voidQuery = voidQuery.Where(x => x.CreatedAt <= to.Value);
            receivingQuery = receivingQuery.Where(x => x.ReceivedDate <= to.Value);
            paymentQuery = paymentQuery.Where(x => x.PaymentDate <= to.Value);
            supplierReturnQuery = supplierReturnQuery.Where(x => x.ReturnDate <= to.Value);
            channelQuery = channelQuery.Where(x => x.SettlementDate <= to.Value);
            consignmentQuery = consignmentQuery.Where(x => x.SettlementDate <= to.Value);
            pettyQuery = pettyQuery.Where(x => x.CreatedAt <= to.Value);
            opnameQuery = opnameQuery.Where(x => x.CreatedAt <= to.Value);
            cashFlowQuery = cashFlowQuery.Where(x => x.TrxDate <= to.Value);
        }

        AddIssues("transaction_sale", await saleQuery.Select(x => x.Id).Take(MaxPreviewIssues).ToListAsync(ct));
        AddIssues("transaction_void", await voidQuery.Select(x => x.Id).Take(MaxPreviewIssues).ToListAsync(ct));
        AddIssues("receiving_note", await receivingQuery.Select(x => x.Id).Take(MaxPreviewIssues).ToListAsync(ct));
        AddIssues("supplier_payment", await paymentQuery.Select(x => x.Id).Take(MaxPreviewIssues).ToListAsync(ct));
        AddIssues("supplier_return", await supplierReturnQuery.Select(x => x.Id).Take(MaxPreviewIssues).ToListAsync(ct));
        AddIssues("channel_settlement", await channelQuery.Select(x => x.Id).Take(MaxPreviewIssues).ToListAsync(ct));
        AddIssues("consignment_settlement", await consignmentQuery.Select(x => x.Id).Take(MaxPreviewIssues).ToListAsync(ct));
        AddIssues("petty_cash_expense", await pettyQuery.Select(x => x.Id).Take(MaxPreviewIssues).ToListAsync(ct));
        AddIssues("stock_opname", await opnameQuery.Select(x => x.Id).Take(MaxPreviewIssues).ToListAsync(ct));
        AddIssues("cash_flow", await cashFlowQuery.Select(x => x.Id).Take(MaxPreviewIssues).ToListAsync(ct));

        void AddIssues(string referenceType, IReadOnlyList<Guid> ids)
        {
            foreach (var id in ids)
                addIssue("SOURCE_NOT_POSTED", referenceType, id, null,
                    $"Dokumen {referenceType} sudah final tetapi belum mempunyai jurnal.",
                    true, null, null, null, null);
        }
    }

    private async Task<IReadOnlyDictionary<string, int>> GetUnpostedCountsAsync(
        Guid businessId, CancellationToken ct)
    {
        var counts = new Dictionary<string, int>
        {
            ["transaction_sale"] = await _dbContext.Transactions
                .CountAsync(x => x.BusinessId == businessId && x.Status == TransactionStatus.Completed &&
                    !_dbContext.JournalEntries.Any(j => j.BusinessId == businessId &&
                        j.ReferenceType == "transaction_sale" && j.ReferenceId == x.Id), ct),
            ["transaction_void"] = await _dbContext.Transactions
                .CountAsync(x => x.BusinessId == businessId && x.Status == TransactionStatus.Voided &&
                    !_dbContext.JournalEntries.Any(j => j.BusinessId == businessId &&
                        j.ReferenceType == "transaction_void" && j.ReferenceId == x.Id), ct),
            ["purchase_order"] = await _dbContext.PurchaseOrders
                .CountAsync(x => x.BusinessId == businessId && x.Status == PurchaseOrderStatus.Completed &&
                    x.PaymentType != PurchaseOrderPaymentType.Consignment &&
                    !_dbContext.ReceivingNotes.Any(r => r.PurchaseOrderId == x.Id) &&
                    !_dbContext.JournalEntries.Any(j => j.BusinessId == businessId &&
                        j.ReferenceType == "purchase_order" && j.ReferenceId == x.Id), ct),
            ["receiving_note"] = await _dbContext.ReceivingNotes
                .CountAsync(x => x.BusinessId == businessId &&
                    x.PurchaseOrder.PaymentType != PurchaseOrderPaymentType.Consignment &&
                    !_dbContext.JournalEntries.Any(j => j.BusinessId == businessId &&
                        j.ReferenceType == "receiving_note" && j.ReferenceId == x.Id), ct),
            ["supplier_payment"] = await _dbContext.SupplierPayments
                .CountAsync(x => x.BusinessId == businessId && x.Status == SupplierPaymentStatus.Paid &&
                    !_dbContext.JournalEntries.Any(j => j.BusinessId == businessId &&
                        j.ReferenceType == "supplier_payment" && j.ReferenceId == x.Id), ct),
            ["supplier_return"] = await _dbContext.SupplierReturns
                .CountAsync(x => x.BusinessId == businessId &&
                    (x.Status == SupplierReturnStatus.Sent || x.Status == SupplierReturnStatus.Completed) &&
                    !_dbContext.JournalEntries.Any(j => j.BusinessId == businessId &&
                        j.ReferenceType == "supplier_return" && j.ReferenceId == x.Id), ct),
            ["channel_settlement"] = await _dbContext.ChannelSettlements
                .CountAsync(x => x.BusinessId == businessId && x.Status == ChannelSettlementStatus.Settled &&
                    !_dbContext.JournalEntries.Any(j => j.BusinessId == businessId &&
                        j.ReferenceType == "channel_settlement" && j.ReferenceId == x.Id), ct),
            ["consignment_settlement"] = await _dbContext.ConsignmentSettlements
                .CountAsync(x => x.BusinessId == businessId && x.Status == ConsignmentSettlementStatus.Settled &&
                    !_dbContext.JournalEntries.Any(j => j.BusinessId == businessId &&
                        j.ReferenceType == "consignment_settlement" && j.ReferenceId == x.Id), ct),
            ["petty_cash_expense"] = await _dbContext.PettyCashExpenses
                .CountAsync(x => x.BusinessId == businessId &&
                    !_dbContext.JournalEntries.Any(j => j.BusinessId == businessId &&
                        j.ReferenceType == "petty_cash_expense" && j.ReferenceId == x.Id), ct),
            ["stock_opname"] = await _dbContext.StockOpnames
                .CountAsync(x => x.BusinessId == businessId && x.Status == "completed" &&
                    !_dbContext.JournalEntries.Any(j => j.BusinessId == businessId &&
                        j.ReferenceType == "stock_opname" && j.ReferenceId == x.Id), ct),
            ["cash_flow"] = await _dbContext.CashFlows
                .CountAsync(x => x.BusinessId == businessId &&
                    !_dbContext.JournalEntries.Any(j => j.BusinessId == businessId &&
                        j.ReferenceType == "cash_flow" && j.ReferenceId == x.Id), ct),
        };

        var postedRefundIds = await _dbContext.JournalEntries.AsNoTracking()
            .Where(x => x.BusinessId == businessId && x.ReferenceType == "transaction_refund")
            .Select(x => x.ReferenceId)
            .ToListAsync(ct);
        var refundIds = await _dbContext.Returns.AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .Select(x => x.RefundRequestId)
            .Distinct()
            .ToListAsync(ct);
        var postedSet = postedRefundIds.ToHashSet();
        counts["transaction_refund"] = refundIds.Count(x => !postedSet.Contains(x));
        return counts;
    }

    private Guid EnsureBusinessContext()
        => _currentUserService.BusinessId
           ?? throw new UnauthorizedAccessException("Business context tidak ditemukan.");
}
