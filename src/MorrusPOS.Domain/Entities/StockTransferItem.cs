using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class StockTransferItem : BaseEntity
{
    // Tenant scope: row milik business ini; FK businesses via konvensi EF
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid StockTransferId { get; set; }
    public StockTransfer StockTransfer { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public Guid? ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }
    public Guid? ProductSerialId { get; set; }
    public ProductSerial? ProductSerial { get; set; }

    public decimal Qty { get; set; }
}
