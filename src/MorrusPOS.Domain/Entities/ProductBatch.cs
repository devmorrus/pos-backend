using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ProductBatch : AuditableEntity
{
    // Tenant scope: row milik business ini; FK businesses via konvensi EF
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public string BatchNumber { get; set; } = default!;
    public DateTime? ExpiryDate { get; set; }

    public decimal QtyProduction { get; set; }
    public decimal QtyRemaining { get; set; }
}
