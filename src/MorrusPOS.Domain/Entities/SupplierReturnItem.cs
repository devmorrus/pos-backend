using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class SupplierReturnItem : BaseEntity
{
    public Guid SupplierReturnId { get; set; }
    public SupplierReturn SupplierReturn { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}
