using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class StockMovementType
{
    public const string OpeningBalance = "opening_balance";
    public const string Sale = "sale";
    public const string Return = "return";
    public const string PurchaseIn = "purchase_in";
    public const string TransferIn = "transfer_in";
    public const string TransferOut = "transfer_out";
    public const string OpnameAdjustment = "opname_adjustment";
    public const string ConsignmentIn = "consignment_in";
    public const string ConsignmentReturn = "consignment_return";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        OpeningBalance, Sale, Return, PurchaseIn, TransferIn, TransferOut,
        OpnameAdjustment, ConsignmentIn, ConsignmentReturn
    };
}

/// <summary>
/// Source of truth pergerakan stok. Setiap insert ke tabel ini akan memicu
/// trigger database yang meng-update InventoryStock.QtyOnHand secara otomatis.
/// ReferenceId sengaja TIDAK di-map sebagai FK (polimorfik: bisa merujuk ke
/// Transaction, PurchaseOrder, StockTransfer, StockOpname, dll).
/// </summary>
public class StockLedger : BaseEntity
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

    public Guid? ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }
    public Guid? ProductSerialId { get; set; }
    public ProductSerial? ProductSerial { get; set; }
    public Guid? DocumentItemId { get; set; }

    public string PostingKey { get; set; } = default!;
    public bool IsSystemGenerated { get; set; }

    public string MovementType { get; set; } = default!;
    public decimal QtyChange { get; set; } // positif = masuk, negatif = keluar

    public string ReferenceType { get; set; } = default!; // "transaction", "purchase_order", dst
    public Guid ReferenceId { get; set; } // polimorfik, tanpa FK constraint

    public string? Note { get; set; }

    public Guid? CreatedBy { get; set; }
    public User? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class StockReconciliationMode
{
    public const string Preview = "preview";
    public const string Repair = "repair";
}

public static class StockReconciliationStatus
{
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Blocked = "blocked";
}

public class StockReconciliationRun : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;
    public Guid RequestedBy { get; set; }
    public User RequestedByUser { get; set; } = default!;
    public string Mode { get; set; } = StockReconciliationMode.Preview;
    public string Status { get; set; } = StockReconciliationStatus.Running;
    public string? Reason { get; set; }
    public int ScannedCount { get; set; }
    public int MismatchCount { get; set; }
    public int RepairableCount { get; set; }
    public int RepairedCount { get; set; }
    public int BlockedCount { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public ICollection<StockReconciliationItem> Items { get; set; } = new List<StockReconciliationItem>();
}

public class StockReconciliationItem : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid RunId { get; set; }
    public StockReconciliationRun Run { get; set; } = default!;
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public string TrackingMode { get; set; } = ProductTrackingModes.Quantity;
    public decimal LedgerQty { get; set; }
    public decimal StoredQty { get; set; }
    public decimal Difference { get; set; }
    public decimal BatchQty { get; set; }
    public int AvailableSerialCount { get; set; }
    public string IssueType { get; set; } = default!;
    public string Action { get; set; } = "none";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StockOpname : BaseEntity
{
    // Tenant scope: row milik business ini; FK businesses via konvensi EF
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
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
    // Tenant scope: row milik business ini; FK businesses via konvensi EF
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid StockOpnameId { get; set; }
    public StockOpname StockOpname { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public Guid? ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }
    public Guid? ProductSerialId { get; set; }
    public ProductSerial? ProductSerial { get; set; }

    public decimal SystemQty { get; set; }
    public decimal PhysicalQty { get; set; }
    public decimal Variance { get; set; } // PhysicalQty - SystemQty
}
