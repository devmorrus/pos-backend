using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class ChartOfAccountType
{
    public const string Asset = "asset";
    public const string Liability = "liability";
    public const string Equity = "equity";
    public const string Revenue = "revenue";
    public const string Cogs = "cogs";
    public const string Expense = "expense";
}

public static class CashFlowType
{
    public const string In = "in";
    public const string Out = "out";
}

public static class AccountingTransactionEntity
{
    public const string Business = "business";
    public const string Outlet = "outlet";
}

public static class AccountingAccountRole
{
    public const string Cash = "cash";
    public const string Bank = "bank";
    public const string AccountsReceivable = "accounts_receivable";
    public const string ChannelClearing = "channel_clearing";
    public const string Inventory = "inventory";
    public const string SalesRevenue = "sales_revenue";
    public const string SalesReturns = "sales_returns";
    public const string ServiceChargeRevenue = "service_charge_revenue";
    public const string TaxPayable = "tax_payable";
    public const string Cogs = "cogs";
    public const string AccountsPayable = "accounts_payable";
    public const string ConsignmentPayable = "consignment_payable";
    public const string ChannelFeeExpense = "channel_fee_expense";
    public const string InventoryGain = "inventory_gain";
    public const string InventoryLoss = "inventory_loss";
    public const string PettyCashExpense = "petty_cash_expense";
    public const string ExchangeClearing = "exchange_clearing";

    public static readonly string[] All =
    [
        Cash, Bank, AccountsReceivable, ChannelClearing, Inventory, SalesRevenue, SalesReturns,
        ServiceChargeRevenue, TaxPayable, Cogs, AccountsPayable, ConsignmentPayable,
        ChannelFeeExpense, InventoryGain, InventoryLoss, PettyCashExpense, ExchangeClearing
    ];
}

public class AccountingAccountMapping : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }
    public string Role { get; set; } = default!;
    public Guid ChartOfAccountId { get; set; }
    public ChartOfAccount ChartOfAccount { get; set; } = default!;
}

public class ChartOfAccount : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }

    public Guid? ParentAccountId { get; set; }
    public ChartOfAccount? ParentAccount { get; set; }

    public string AccountCode { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    public string AccountType { get; set; } = ChartOfAccountType.Asset;
    public bool IsCashBank { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ChartOfAccount> Children { get; set; } = new List<ChartOfAccount>();
    public ICollection<CashFlow> SourceCashFlows { get; set; } = new List<CashFlow>();
    public ICollection<CashFlow> DestinationCashFlows { get; set; } = new List<CashFlow>();
    public ICollection<AccountTransaction> AccountTransactions { get; set; } = new List<AccountTransaction>();
}

public class CashFlow : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }

    public string TrxNumber { get; set; } = default!;
    public DateTime TrxDate { get; set; } = DateTime.UtcNow;
    public string TrxType { get; set; } = CashFlowType.In;
    public string TrxEntity { get; set; } = AccountingTransactionEntity.Business;

    public Guid? FromChartOfAccountId { get; set; }
    public ChartOfAccount? FromChartOfAccount { get; set; }

    public Guid? ToChartOfAccountId { get; set; }
    public ChartOfAccount? ToChartOfAccount { get; set; }

    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public string? AttachmentUrl { get; set; }

    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = default!;
}

public class AccountTransaction : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }

    public Guid JournalEntryId { get; set; }
    public JournalEntry JournalEntry { get; set; } = default!;
    public int LineNumber { get; set; }

    public DateTime TrxDate { get; set; } = DateTime.UtcNow;
    public string TrxNumber { get; set; } = default!;
    public string ReferenceType { get; set; } = default!;
    public Guid? ReferenceId { get; set; }
    public string TrxEntity { get; set; } = AccountingTransactionEntity.Business;

    public Guid ChartOfAccountId { get; set; }
    public ChartOfAccount ChartOfAccount { get; set; } = default!;

    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Note { get; set; }
}

public static class JournalEntryStatus
{
    public const string Posted = "posted";
    public const string Reversed = "reversed";
}

public class JournalEntry : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }
    public string JournalNumber { get; set; } = default!;
    public DateTime JournalDate { get; set; }
    public string ReferenceType { get; set; } = default!;
    public Guid ReferenceId { get; set; }
    public string Description { get; set; } = default!;
    public string Status { get; set; } = JournalEntryStatus.Posted;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public string PostingHash { get; set; } = default!;
    public Guid? ReversalOfJournalId { get; set; }
    public JournalEntry? ReversalOfJournal { get; set; }
    public Guid? CreatedBy { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<AccountTransaction> Lines { get; set; } = new List<AccountTransaction>();
    public ICollection<JournalEntry> Reversals { get; set; } = new List<JournalEntry>();
}
