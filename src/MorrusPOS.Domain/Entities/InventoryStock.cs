using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

/// <summary>
/// Snapshot/cache qty stok saat ini. Sumber kebenaran tetap StockLedger.
/// qty_on_hand di-update via DATABASE TRIGGER (bukan service layer) — lihat
/// Infrastructure/Persistence/Migrations untuk raw SQL trigger-nya.
/// </summary>
public class InventoryStock : BaseEntity
{
    // Tenant scope: row milik business ini; FK businesses via konvensi EF
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public Guid StockLocationId { get; set; }
    public StockLocation StockLocation { get; set; } = default!;

    public decimal QtyOnHand { get; set; }
    public decimal MinStockAlert { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
