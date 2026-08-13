using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class CashFlowPostingService : ICashFlowPostingService
{
    private readonly AppDbContext _dbContext;

    public CashFlowPostingService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CashFlowJournalEntryDto>> PostAsync(
        CashFlow cashFlow,
        ChartOfAccount fromAccount,
        ChartOfAccount toAccount,
        CancellationToken ct = default)
    {
        var note = string.IsNullOrWhiteSpace(cashFlow.Note)
            ? cashFlow.TrxType == CashFlowType.In
                ? $"Pemasukan toko {cashFlow.TrxNumber}"
                : $"Pengeluaran toko {cashFlow.TrxNumber}"
            : cashFlow.Note!.Trim();

        var entries = BuildTransactions(cashFlow, fromAccount, toAccount, note);
        _dbContext.AccountTransactions.AddRange(entries);
        await _dbContext.SaveChangesAsync(ct);

        return entries
            .Select(entry => new CashFlowJournalEntryDto(
                entry.Id,
                entry.ChartOfAccountId,
                entry.ChartOfAccount.AccountCode,
                entry.ChartOfAccount.AccountName,
                entry.DebitAmount,
                entry.CreditAmount))
            .ToList();
    }

    private static List<AccountTransaction> BuildTransactions(
        CashFlow cashFlow,
        ChartOfAccount fromAccount,
        ChartOfAccount toAccount,
        string note)
    {
        var fromIsCashBank = fromAccount.IsCashBank;
        var toIsCashBank = toAccount.IsCashBank;

        if (!fromIsCashBank && !toIsCashBank)
        {
            throw new InvalidOperationException("Salah satu akun harus bertipe kas/bank.");
        }

        return cashFlow.TrxType switch
        {
            CashFlowType.In => BuildIncomeTransactions(cashFlow, fromAccount, toAccount, fromIsCashBank, note),
            CashFlowType.Out => BuildOutcomeTransactions(cashFlow, fromAccount, toAccount, fromIsCashBank, note),
            _ => throw new InvalidOperationException("Tipe cash flow tidak valid."),
        };
    }

    private static List<AccountTransaction> BuildIncomeTransactions(
        CashFlow cashFlow,
        ChartOfAccount fromAccount,
        ChartOfAccount toAccount,
        bool fromIsCashBank,
        string note)
    {
        var debitAccount = fromIsCashBank ? fromAccount : toAccount;
        var creditAccount = fromIsCashBank ? toAccount : fromAccount;

        return BuildDoubleEntry(cashFlow, debitAccount, creditAccount, note);
    }

    private static List<AccountTransaction> BuildOutcomeTransactions(
        CashFlow cashFlow,
        ChartOfAccount fromAccount,
        ChartOfAccount toAccount,
        bool fromIsCashBank,
        string note)
    {
        var creditAccount = fromIsCashBank ? fromAccount : toAccount;
        var debitAccount = fromIsCashBank ? toAccount : fromAccount;

        return BuildDoubleEntry(cashFlow, debitAccount, creditAccount, note);
    }

    private static List<AccountTransaction> BuildDoubleEntry(
        CashFlow cashFlow,
        ChartOfAccount debitAccount,
        ChartOfAccount creditAccount,
        string note)
    {
        var debit = new AccountTransaction
        {
            Id = Guid.NewGuid(),
            BusinessId = cashFlow.BusinessId,
            OutletId = cashFlow.OutletId,
            TrxDate = cashFlow.TrxDate,
            TrxNumber = cashFlow.TrxNumber,
            ReferenceType = "cash_flow",
            ReferenceId = cashFlow.Id,
            TrxEntity = AccountingTransactionEntity.Business,
            ChartOfAccountId = debitAccount.Id,
            ChartOfAccount = debitAccount,
            DebitAmount = cashFlow.Amount,
            CreditAmount = 0,
            Note = note,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var credit = new AccountTransaction
        {
            Id = Guid.NewGuid(),
            BusinessId = cashFlow.BusinessId,
            OutletId = cashFlow.OutletId,
            TrxDate = cashFlow.TrxDate,
            TrxNumber = cashFlow.TrxNumber,
            ReferenceType = "cash_flow",
            ReferenceId = cashFlow.Id,
            TrxEntity = AccountingTransactionEntity.Business,
            ChartOfAccountId = creditAccount.Id,
            ChartOfAccount = creditAccount,
            DebitAmount = 0,
            CreditAmount = cashFlow.Amount,
            Note = note,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return [debit, credit];
    }
}
