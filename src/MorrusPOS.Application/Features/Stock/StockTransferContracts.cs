namespace MorrusPOS.Application.Features.Stock;

public record StockTransferItemDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal Qty
)
{
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
);

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
    List<StockTransferItemRequest> Items
);

public interface IStockTransferService
{
    Task<StockTransferDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StockTransferDto>> GetOutgoingTransfersAsync(Guid stockLocationId, CancellationToken ct = default);
    Task<IReadOnlyList<StockTransferDto>> GetIncomingTransfersAsync(Guid stockLocationId, CancellationToken ct = default);
    Task<StockTransferDto> CreateAsync(Guid userId, CreateStockTransferRequest request, CancellationToken ct = default);
    Task<StockTransferDto> ApproveAsync(Guid userId, Guid transferId, CancellationToken ct = default);
    Task<StockTransferDto> RejectAsync(Guid userId, Guid transferId, CancellationToken ct = default);
}
