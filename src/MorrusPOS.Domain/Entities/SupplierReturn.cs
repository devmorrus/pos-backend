using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class SupplierReturnStatus
{
    public const string Draft = "draft";
    public const string Sent = "sent";
    public const string Completed = "completed";
}

public static class SupplierReturnSettlement
{
    // Tempo return reduces accounts payable.
    public const string Payable = "payable";
    // Cash purchase return where the supplier has not refunded yet.
    public const string Receivable = "receivable";
    // Cash purchase return where the refund has been received.
    public const string CashRefund = "cash_refund";
}

public class SupplierReturn : AuditableEntity
{
    // Tenant scope: row milik business ini; FK businesses via konvensi EF
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = default!;

    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = default!;

    public string ReturnNumber { get; set; } = default!;
    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = SupplierReturnStatus.Draft;
    public decimal TotalAmount { get; set; }
    public string SettlementTreatment { get; set; } = SupplierReturnSettlement.Payable;
    public string? Notes { get; set; }

    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = default!;

    public ICollection<SupplierReturnItem> Items { get; set; } = new List<SupplierReturnItem>();
}
