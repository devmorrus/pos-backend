using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class StockTransferItem : BaseEntity
{
    public Guid StockTransferId { get; set; }
    public StockTransfer StockTransfer { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public decimal Qty { get; set; }
}
