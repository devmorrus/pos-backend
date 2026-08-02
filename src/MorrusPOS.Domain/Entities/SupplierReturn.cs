using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class SupplierReturnStatus
{
    public const string Draft = "draft";
    public const string Sent = "sent";
    public const string Completed = "completed";
}

public class SupplierReturn : AuditableEntity
{
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = default!;

    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = default!;

    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = SupplierReturnStatus.Draft;

    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = default!;

    public ICollection<SupplierReturnItem> Items { get; set; } = new List<SupplierReturnItem>();
}
