using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class IdempotencyOperation
{
    public const string Checkout = "checkout";
    public const string Refund = "refund";
    public const string SupplierPayment = "supplier_payment";
    public const string StockTransferSend = "stock_transfer_send";
    public const string StockTransferReceive = "stock_transfer_receive";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Checkout, Refund, SupplierPayment, StockTransferSend, StockTransferReceive
    };
}

public class IdempotencyRecord : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid IdempotencyKey { get; set; }
    public string Operation { get; set; } = default!;
    public string RequestHash { get; set; } = default!;
    public string ResourceType { get; set; } = default!;
    public Guid ResourceId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
