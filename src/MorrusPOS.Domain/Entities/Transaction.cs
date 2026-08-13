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

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string TransactionNumber { get; set; } = default!; // TRX-20260731-0001
    public string Channel { get; set; } = TransactionChannel.Pos;
    public string Status { get; set; } = TransactionStatus.Pending;
    public string CustomerType { get; set; } = TransactionCustomerType.Guest;
    public string? CustomerNameSnapshot { get; set; }
    public string? CustomerPhoneSnapshot { get; set; }
    public string? ExternalCustomerReference { get; set; }
    public string? ExternalCustomerName { get; set; }
    public string? ExternalCustomerPhone { get; set; }
    public string? LoyaltyReference { get; set; }
    public string? ChannelOrderReference { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal ManualDiscountTotal { get; set; }
    public decimal PromoDiscountTotal { get; set; }
    public decimal VoucherDiscountTotal { get; set; }
    public decimal ServiceChargeTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public string? AppliedVoucherCode { get; set; }
    public string? AppliedPromoName { get; set; }

    public Guid? VoidedBy { get; set; }
    public User? VoidedByUser { get; set; }
    public string? VoidedReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TransactionItem> Items { get; set; } = new List<TransactionItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Return> Returns { get; set; } = new List<Return>();
    public ICollection<VoucherRedemption> VoucherRedemptions { get; set; } = new List<VoucherRedemption>();
}

public class TransactionItem : BaseEntity
{
    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }   // snapshot harga jual saat transaksi
    public decimal UnitCost { get; set; }    // snapshot harga modal, untuk laporan margin
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsReturned { get; set; } = false;

    public string? SelectedModifiersJson { get; set; } // Extra details like toppings or specific customization
}
