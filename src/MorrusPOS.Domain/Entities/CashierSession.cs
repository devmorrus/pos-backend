using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class CashierSessionStatus
{
    public const string Open = "open";
    public const string Closed = "closed";
}

public class CashierSession : AuditableEntity
{
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public DateTime OpeningTime { get; set; } = DateTime.UtcNow;
    public DateTime? ClosingTime { get; set; }

    public decimal OpeningCash { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal? ActualCash { get; set; }
    public decimal? Variance { get; set; }

    public string Status { get; set; } = CashierSessionStatus.Open;

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<PettyCashExpense> PettyCashExpenses { get; set; } = new List<PettyCashExpense>();
}
