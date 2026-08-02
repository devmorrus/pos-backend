using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ChannelSettlementItem : BaseEntity
{
    public Guid ChannelSettlementId { get; set; }
    public ChannelSettlement ChannelSettlement { get; set; } = default!;

    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = default!;

    public decimal GrossAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal NetAmount { get; set; }
}
