using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class PaymentMethod
{
    public const string Cash = "cash";
    public const string Qris = "qris";
    public const string Transfer = "transfer";
    public const string Edc = "edc";
}

public class Payment : BaseEntity
{
    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = default!;

    public Guid? CashierSessionId { get; set; }
    public CashierSession? CashierSession { get; set; }

    public string Method { get; set; } = default!;
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Return : BaseEntity
{
    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = default!;

    public Guid TransactionItemId { get; set; }
    public TransactionItem TransactionItem { get; set; } = default!;

    public decimal Qty { get; set; }
    public string? Reason { get; set; }
    public string RefundMethod { get; set; } = default!; // "refund", "exchange"

    public Guid ProcessedBy { get; set; }
    public User ProcessedByUser { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
