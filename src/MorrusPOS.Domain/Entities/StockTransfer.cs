using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class StockTransferStatus
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Cancelled = "cancelled";
    public const string Rejected = "rejected";
}

public class StockTransfer : AuditableEntity
{
    public Guid FromOutletId { get; set; }
    public Outlet FromOutlet { get; set; } = default!;

    public Guid ToOutletId { get; set; }
    public Outlet ToOutlet { get; set; } = default!;

    public string TransferNumber { get; set; } = default!;
    public string Status { get; set; } = StockTransferStatus.Pending;

    public Guid RequestedBy { get; set; }
    public User RequestedByUser { get; set; } = default!;

    public Guid? ApprovedBy { get; set; }
    public User? ApprovedByUser { get; set; }

    public ICollection<StockTransferItem> Items { get; set; } = new List<StockTransferItem>();
}
