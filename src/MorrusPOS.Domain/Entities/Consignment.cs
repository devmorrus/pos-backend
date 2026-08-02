using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class ConsignmentStatus
{
    public const string Draft = "draft";
    public const string Received = "received";
    public const string Cancelled = "cancelled";
}

public class Consignment : AuditableEntity
{
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = default!;

    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public string ConsignmentNumber { get; set; } = default!;
    public DateTime ReceiveDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = ConsignmentStatus.Draft;

    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = default!;

    public ICollection<ConsignmentItem> Items { get; set; } = new List<ConsignmentItem>();
}
