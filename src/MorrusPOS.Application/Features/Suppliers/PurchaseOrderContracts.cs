namespace MorrusPOS.Application.Features.Suppliers;

public record PurchaseOrderItemDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal Qty,
    decimal UnitCost,
    decimal TotalCost
);

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
);

public record PurchaseOrderItemRequest(
    Guid ProductId,
    decimal Qty,
    decimal UnitCost
);

public record CreatePurchaseOrderRequest(
    Guid SupplierId,
    Guid OutletId,
    string PaymentType,
    DateTime? DueDate,
    List<PurchaseOrderItemRequest> Items
);

public record UpdatePoStatusRequest(
    string Status
);

public interface IPurchaseOrderService
{
    Task<PurchaseOrderDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseOrderDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default);
    Task<PurchaseOrderDto> CreateAsync(Guid userId, CreatePurchaseOrderRequest request, CancellationToken ct = default);
    Task<PurchaseOrderDto> UpdateStatusAsync(Guid userId, Guid poId, UpdatePoStatusRequest request, CancellationToken ct = default);
}
