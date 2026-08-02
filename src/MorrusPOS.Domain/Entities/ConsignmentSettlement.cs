using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class ConsignmentSettlementStatus
{
    public const string Draft = "draft";
    public const string Settled = "settled";
    public const string Cancelled = "cancelled";
}

public class ConsignmentSettlement : AuditableEntity
{
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = default!;

    public string SettlementNumber { get; set; } = default!;
    public DateTime SettlementDate { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = ConsignmentSettlementStatus.Draft;

    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = default!;

    public ICollection<ConsignmentSale> Sales { get; set; } = new List<ConsignmentSale>();
}
