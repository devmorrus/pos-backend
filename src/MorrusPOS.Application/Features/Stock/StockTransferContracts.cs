namespace MorrusPOS.Application.Features.Stock;

public record StockTransferItemDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal Qty
);

public record StockTransferDto(
    Guid Id,
    Guid FromOutletId,
    string FromOutletName,
    Guid ToOutletId,
    string ToOutletName,
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
);

public record CreateStockTransferRequest(
    Guid FromOutletId,
    Guid ToOutletId,
    List<StockTransferItemRequest> Items
);

public interface IStockTransferService
{
    Task<StockTransferDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StockTransferDto>> GetOutgoingTransfersAsync(Guid outletId, CancellationToken ct = default);
    Task<IReadOnlyList<StockTransferDto>> GetIncomingTransfersAsync(Guid outletId, CancellationToken ct = default);
    Task<StockTransferDto> CreateAsync(Guid userId, CreateStockTransferRequest request, CancellationToken ct = default);
    Task<StockTransferDto> ApproveAsync(Guid userId, Guid transferId, CancellationToken ct = default);
    Task<StockTransferDto> RejectAsync(Guid userId, Guid transferId, CancellationToken ct = default);
}
