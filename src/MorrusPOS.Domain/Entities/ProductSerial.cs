using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class ProductSerialStatuses
{
    public const string Available = "available";
    public const string Reserved = "reserved";
    public const string Sold = "sold";
    public const string InTransfer = "in_transfer";
    public const string ReturnedToSupplier = "returned_to_supplier";
    public const string WrittenOff = "written_off";
}

public class ProductSerial : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public Guid StockLocationId { get; set; }
    public StockLocation StockLocation { get; set; } = default!;
    public Guid? ReceivingNoteItemId { get; set; }
    public ReceivingNoteItem? ReceivingNoteItem { get; set; }
    public string SerialNumber { get; set; } = default!;
    public string NormalizedSerialNumber { get; set; } = default!;
    public string Status { get; set; } = ProductSerialStatuses.Available;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SoldAt { get; set; }
}
