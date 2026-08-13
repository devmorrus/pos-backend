using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ReceivingNote : AuditableEntity
{
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = default!;

    public string ReceivingNumber { get; set; } = default!;
    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public Guid ReceivedBy { get; set; }
    public User ReceivedByUser { get; set; } = default!;

    public ICollection<ReceivingNoteItem> Items { get; set; } = new List<ReceivingNoteItem>();
}
