namespace MorrusPOS.Application.Features.Stock;

public record StockTransferItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal Qty
)
{
    public decimal SentQty => Qty;
    public decimal? ReceivedQty { get; init; }
    public decimal? DifferenceQty { get; init; }
    public string? VarianceReasonCode { get; init; }
    public string? VarianceNote { get; init; }
    public decimal UnitCostSnapshot { get; init; }
    public decimal? VarianceValue => DifferenceQty * UnitCostSnapshot;
    public Guid? ProductVariantId { get; init; }
    public Guid? ProductBatchId { get; init; }
    public string? BatchNumber { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public Guid? ProductSerialId { get; init; }
    public string? SerialNumber { get; init; }
}

public record StockTransferDto(
    Guid Id,
    Guid FromStockLocationId,
    string FromStockLocationName,
    string FromStockLocationType,
    Guid? FromOutletId,
    Guid ToStockLocationId,
    string ToStockLocationName,
    string ToStockLocationType,
    Guid? ToOutletId,
    string TransferNumber,
    string Status,
    Guid RequestedBy,
    string RequestedByName,
    Guid? ApprovedBy,
    string? ApprovedByName,
    DateTime CreatedAt,
    IReadOnlyList<StockTransferItemDto> Items
)
{
    public DateTime? ReceivedAt { get; init; }
    public string? ReceiptNote { get; init; }
    public bool HasVariance => Items.Any(x => x.DifferenceQty.HasValue && x.DifferenceQty.Value != 0);
    public decimal TotalSentQty => Items.Sum(x => x.Qty);
    public decimal? TotalReceivedQty => Items.All(x => x.ReceivedQty.HasValue) ? Items.Sum(x => x.ReceivedQty!.Value) : null;
    public decimal? TotalDifferenceQty => Items.All(x => x.DifferenceQty.HasValue) ? Items.Sum(x => x.DifferenceQty!.Value) : null;
}

public record StockTransferItemRequest(
    Guid ProductId,
    decimal Qty
)
{
    public Guid? ProductVariantId { get; init; }
    public Guid? ProductBatchId { get; init; }
    public IReadOnlyList<Guid>? ProductSerialIds { get; init; }
}

public record CreateStockTransferRequest(
    Guid FromStockLocationId,
    Guid ToStockLocationId,
    List<StockTransferItemRequest> Items,
    Guid RequestId = default
);

public record ReceiveStockTransferItemRequest(
    Guid TransferItemId,
    decimal ReceivedQty,
    string? VarianceReasonCode = null,
    string? VarianceNote = null
);

public record ReceiveStockTransferRequest(
    Guid RequestId,
    IReadOnlyList<ReceiveStockTransferItemRequest> Items,
    string? ReceiptNote = null
);

public interface IStockTransferService
{
    Task<StockTransferDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StockTransferDto>> GetOutgoingTransfersAsync(Guid stockLocationId, CancellationToken ct = default);
    Task<IReadOnlyList<StockTransferDto>> GetIncomingTransfersAsync(Guid stockLocationId, CancellationToken ct = default);
    Task<StockTransferDto> CreateAsync(Guid userId, CreateStockTransferRequest request, CancellationToken ct = default);
    Task<StockTransferDto> ReceiveAsync(Guid userId, Guid transferId, ReceiveStockTransferRequest request, CancellationToken ct = default);
    Task<StockTransferDto> ApproveAsync(Guid userId, Guid transferId, CancellationToken ct = default);
    Task<StockTransferDto> RejectAsync(Guid userId, Guid transferId, CancellationToken ct = default);
}
