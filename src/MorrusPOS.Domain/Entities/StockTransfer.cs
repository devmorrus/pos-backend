using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class StockTransferStatus
{
    public const string InTransit = "in_transit";
    public const string Received = "received";
    public const string ReceivedWithVariance = "received_with_variance";
    public const string Returned = "returned";
    // Legacy values kept while older clients/data are migrated.
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Cancelled = "cancelled";
    public const string Rejected = "rejected";
}

public class StockTransfer : AuditableEntity
{
    // Tenant scope: row milik business ini; FK businesses via konvensi EF
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid FromStockLocationId { get; set; }
    public StockLocation FromStockLocation { get; set; } = default!;

    public Guid ToStockLocationId { get; set; }
    public StockLocation ToStockLocation { get; set; } = default!;

    public string TransferNumber { get; set; } = default!;
    public string Status { get; set; } = StockTransferStatus.InTransit;

    public Guid RequestedBy { get; set; }
    public User RequestedByUser { get; set; } = default!;

    public Guid? ApprovedBy { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public string? ReceiptNote { get; set; }

    public ICollection<StockTransferItem> Items { get; set; } = new List<StockTransferItem>();
}
