using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ConsignmentItem : BaseEntity
{
    public Guid ConsignmentId { get; set; }
    public Consignment Consignment { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SoldQty { get; set; } = 0;
    public decimal ReturnedQty { get; set; } = 0;
}
