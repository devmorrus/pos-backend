using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class StockReservationStatus
{
    public const string Active = "active";
    public const string Consumed = "consumed";
    public const string Released = "released";
    public const string Expired = "expired";
}

public static class StockReservationSource
{
    public const string Pos = "pos";
    public const string ChannelOrder = "channel_order";
}

public class StockReservation : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid StockLocationId { get; set; }
    public StockLocation StockLocation { get; set; } = default!;
    public Guid? CashierSessionId { get; set; }
    public CashierSession? CashierSession { get; set; }
    public Guid? RegisterId { get; set; }
    public PosRegister? Register { get; set; }
    public Guid? DeviceId { get; set; }
    public PosDevice? Device { get; set; }
    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = default!;
    public string SourceType { get; set; } = StockReservationSource.Pos;
    public string Status { get; set; } = StockReservationStatus.Active;
    public string RequestHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ConsumedByTransactionId { get; set; }
    public Transaction? ConsumedByTransaction { get; set; }
    public string? ReleaseReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<StockReservationItem> Items { get; set; } = new List<StockReservationItem>();
}

public class StockReservationItem : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid StockReservationId { get; set; }
    public StockReservation StockReservation { get; set; } = default!;
    public Guid StockLocationId { get; set; }
    public StockLocation StockLocation { get; set; } = default!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public Guid? ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }
    public Guid? ProductSerialId { get; set; }
    public ProductSerial? ProductSerial { get; set; }
    public string TrackingModeSnapshot { get; set; } = ProductTrackingModes.Quantity;
    public decimal Qty { get; set; }
    public string Status { get; set; } = StockReservationStatus.Active;
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
