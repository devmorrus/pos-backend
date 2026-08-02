using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class ConsignmentSaleStatus
{
    public const string Unpaid = "unpaid";
    public const string Paid = "paid";
}

public class ConsignmentSale : BaseEntity
{
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = default!;

    public Guid TransactionItemId { get; set; }
    public TransactionItem TransactionItem { get; set; } = default!;

    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = ConsignmentSaleStatus.Unpaid;

    public Guid? ConsignmentSettlementId { get; set; }
    public ConsignmentSettlement? ConsignmentSettlement { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
