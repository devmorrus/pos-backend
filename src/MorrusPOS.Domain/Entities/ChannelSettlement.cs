using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class ChannelSettlementStatus
{
    public const string Pending = "pending";
    public const string Settled = "settled";
    public const string Cancelled = "cancelled";
}

public class ChannelSettlement : AuditableEntity
{
    public Guid ChannelAccountId { get; set; }
    public ChannelAccount ChannelAccount { get; set; } = default!;

    public string SettlementNumber { get; set; } = default!; // e.g. SET-OL-20260731-0001
    public DateTime SettlementDate { get; set; } = DateTime.UtcNow;
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }

    public decimal GrossAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal NetAmount { get; set; }

    public string Status { get; set; } = ChannelSettlementStatus.Pending;

    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = default!;

    public ICollection<ChannelSettlementItem> Items { get; set; } = new List<ChannelSettlementItem>();
}
