namespace MorrusPOS.Application.Features.Suppliers;

public record PurchaseOrderItemDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal Qty,
    decimal UnitCost,
    decimal TotalCost,
    decimal SellingPrice
)
{
    public Guid? ProductVariantId { get; init; }
    public decimal QtyReceived { get; init; }
    public string TrackingMode { get; init; } = "quantity";
}

public record PurchaseOrderDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    Guid OutletId,
    string OutletName,
    string PoNumber,
    DateTime PoDate,
    string PaymentType,
    string Status,
    DateTime? DueDate,
    decimal TotalAmount,
    IReadOnlyList<PurchaseOrderItemDto> Items
)
{
    public Guid? DestinationStockLocationId { get; init; }
    public string? DestinationStockLocationName { get; init; }
}

public record PurchaseOrderItemRequest(
    Guid ProductId,
    decimal Qty,
    decimal UnitCost
)
{
    public Guid? ProductVariantId { get; init; }
}

public record CreatePurchaseOrderRequest(
    Guid SupplierId,
    Guid OutletId,
    string PaymentType,
    DateTime? DueDate,
    List<PurchaseOrderItemRequest> Items
)
{
    public Guid? DestinationStockLocationId { get; init; }
}

public record UpdatePoStatusRequest(
    string Status
);

public record ReceivingItemRequest(
    Guid ProductId,
    Guid? ProductVariantId,
    decimal QtyReceived,
    string? BatchNumber,
    DateTime? ExpiryDate)
{
    public IReadOnlyList<ReceivingBatchRequest>? Batches { get; init; }
    public IReadOnlyList<string>? SerialNumbers { get; init; }
}

public record ReceivingBatchRequest(string BatchNumber, DateTime? ExpiryDate, decimal Qty);

public record ReceiveGoodsRequest(
    Guid OutletId,
    string? Notes,
    List<ReceivingItemRequest> Items
);

public interface IPurchaseOrderService
{
    Task<PurchaseOrderDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseOrderDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default);
    Task<PurchaseOrderDto> CreateAsync(Guid userId, CreatePurchaseOrderRequest request, CancellationToken ct = default);
    Task<PurchaseOrderDto> UpdateStatusAsync(Guid userId, Guid poId, UpdatePoStatusRequest request, CancellationToken ct = default);
    Task<PurchaseOrderDto> ReceiveGoodsAsync(Guid userId, Guid poId, ReceiveGoodsRequest request, CancellationToken ct = default);
}
