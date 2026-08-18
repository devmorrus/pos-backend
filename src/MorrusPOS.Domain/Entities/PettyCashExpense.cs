using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class PettyCashExpense : AuditableEntity
{
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public Guid CashierSessionId { get; set; }
    public CashierSession CashierSession { get; set; } = default!;

    public decimal Amount { get; set; }
    public string Description { get; set; } = default!;
    public string Category { get; set; } = default!; // "ATK", "Konsumsi", "Kemasan", "Operasional", "Lain-lain"

    public Guid ProcessedBy { get; set; }
    public User ProcessedByUser { get; set; } = default!;
}
