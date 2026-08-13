using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class PurchaseOrderStatus
{
    public const string Draft = "draft";
    public const string Pending = "pending";
    public const string PartiallyReceived = "partially_received";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}

public static class PurchaseOrderPaymentType
{
    public const string Cash = "cash";
    public const string Tempo = "tempo";
    public const string Consignment = "consignment";
}

public class PurchaseOrder : AuditableEntity
{
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = default!;

    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public string PoNumber { get; set; } = default!;
    public DateTime PoDate { get; set; } = DateTime.UtcNow;
    
    public string PaymentType { get; set; } = PurchaseOrderPaymentType.Cash;
    public string Status { get; set; } = PurchaseOrderStatus.Draft;

    public DateTime? DueDate { get; set; }
    public decimal TotalAmount { get; set; }

    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = default!;

    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}
