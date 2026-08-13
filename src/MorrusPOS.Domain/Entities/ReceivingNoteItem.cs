using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ReceivingNoteItem : BaseEntity
{
    public Guid ReceivingNoteId { get; set; }
    public ReceivingNote ReceivingNote { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public decimal QtyReceived { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
