using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class SupplierPaymentStatus
{
    public const string Paid = "paid";
    public const string Cancelled = "cancelled";
}

public class SupplierPayment : BaseEntity
{
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = default!;

    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = default!;

    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = default!; // e.g. Cash, Transfer
    public string? ReferenceNumber { get; set; }

    public string Status { get; set; } = SupplierPaymentStatus.Paid;

    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
