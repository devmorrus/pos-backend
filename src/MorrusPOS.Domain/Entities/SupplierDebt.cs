using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class SupplierDebtStatus
{
    public const string Unpaid = "unpaid";
    public const string PartiallyPaid = "partially_paid";
    public const string Paid = "paid";
}

public class SupplierDebt : AuditableEntity
{
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = default!;

    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = default!;

    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    
    public string Status { get; set; } = SupplierDebtStatus.Unpaid;
}
