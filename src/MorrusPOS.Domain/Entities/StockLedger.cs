using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class StockMovementType
{
    public const string Sale = "sale";
    public const string Return = "return";
    public const string PurchaseIn = "purchase_in";
    public const string TransferIn = "transfer_in";
    public const string TransferOut = "transfer_out";
    public const string OpnameAdjustment = "opname_adjustment";
    public const string ConsignmentIn = "consignment_in";
    public const string ConsignmentReturn = "consignment_return";
}

/// <summary>
/// Source of truth pergerakan stok. Setiap insert ke tabel ini akan memicu
/// trigger database yang meng-update InventoryStock.QtyOnHand secara otomatis.
/// ReferenceId sengaja TIDAK di-map sebagai FK (polimorfik: bisa merujuk ke
/// Transaction, PurchaseOrder, StockTransfer, StockOpname, dll).
/// </summary>
public class StockLedger : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public string MovementType { get; set; } = default!;
    public decimal QtyChange { get; set; } // positif = masuk, negatif = keluar

    public string ReferenceType { get; set; } = default!; // "transaction", "purchase_order", dst
    public Guid ReferenceId { get; set; } // polimorfik, tanpa FK constraint

    public string? Note { get; set; }

    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StockOpname : BaseEntity
{
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public Guid PerformedBy { get; set; }
    public User PerformedByUser { get; set; } = default!;

    public string Status { get; set; } = "draft"; // draft, completed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<StockOpnameItem> Items { get; set; } = new List<StockOpnameItem>();
}

public class StockOpnameItem : BaseEntity
{
    public Guid StockOpnameId { get; set; }
    public StockOpname StockOpname { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public decimal SystemQty { get; set; }
    public decimal PhysicalQty { get; set; }
    public decimal Variance { get; set; } // PhysicalQty - SystemQty
}
