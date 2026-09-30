using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

/// <summary>
/// Kebijakan buffer stock (safety stock) per outlet + produk (+ varian).
/// Buffer TIDAK mengurangi QtyOnHand dan TIDAK menulis StockLedger.
/// Rumus stok online: AvailableOnlineQty = max(0, QtyOnHand - BufferQty).
/// Jika tidak ada row policy, BufferQty dianggap 0 (produk tetap tersedia online).
/// </summary>
public class ChannelStockPolicy : BaseEntity
{
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    /// <summary>
    /// Jumlah stok pengaman. Tidak boleh negatif (divalidasi di service/validator).
    /// </summary>
    public decimal BufferQty { get; set; }

    /// <summary>
    /// Jika false, buffer dianggap 0 (tidak memengaruhi availability).
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    public Guid UpdatedBy { get; set; }
    public User UpdatedByUser { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
