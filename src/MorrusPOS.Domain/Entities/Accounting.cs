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
