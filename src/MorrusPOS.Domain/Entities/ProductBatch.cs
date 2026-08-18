using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ProductBatch : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public string BatchNumber { get; set; } = default!;
    public DateTime ExpiryDate { get; set; }

    public decimal QtyProduction { get; set; }
    public decimal QtyRemaining { get; set; }
}
