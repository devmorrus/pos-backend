using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ReceivingNoteItem : BaseEntity
{
    // Tenant scope: row milik business ini; FK businesses via konvensi EF
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
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
