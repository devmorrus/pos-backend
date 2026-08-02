using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class TransactionStatus
{
    public const string Pending = "pending";
    public const string Completed = "completed";
    public const string Voided = "voided";
    public const string Refunded = "refunded";
}

public static class TransactionChannel
{
    public const string Pos = "pos";
    public const string GoFood = "gofood";
    public const string GrabFood = "grabfood";
    public const string ShopeeFood = "shopeefood";
}

public class Transaction : BaseEntity
{
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public Guid? CashierSessionId { get; set; }
    public CashierSession? CashierSession { get; set; }

    public string TransactionNumber { get; set; } = default!; // TRX-20260731-0001
    public string Channel { get; set; } = TransactionChannel.Pos;
    public string Status { get; set; } = TransactionStatus.Pending;

    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }

    public Guid? VoidedBy { get; set; }
    public User? VoidedByUser { get; set; }
    public string? VoidedReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TransactionItem> Items { get; set; } = new List<TransactionItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Return> Returns { get; set; } = new List<Return>();
}

public class TransactionItem : BaseEntity
{
    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }   // snapshot harga jual saat transaksi
    public decimal UnitCost { get; set; }    // snapshot harga modal, untuk laporan margin
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsReturned { get; set; } = false;
}
