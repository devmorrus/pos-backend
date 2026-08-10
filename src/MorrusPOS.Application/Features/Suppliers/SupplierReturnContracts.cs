namespace MorrusPOS.Application.Features.Suppliers;

public record SupplierReturnItemDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal Qty,
    decimal UnitCost,
    decimal LineTotal,
    decimal EligibleQty
);

public record SupplierReturnListItemDto(
    Guid Id,
    string ReturnNumber,
    Guid SupplierId,
    string SupplierName,
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    Guid OutletId,
    string OutletName,
    DateTime ReturnDate,
    decimal TotalAmount,
    string Status,
    string CreatedByName
);

public record SupplierReturnDto(
    Guid Id,
    string ReturnNumber,
    Guid SupplierId,
    string SupplierName,
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    Guid OutletId,
    string OutletName,
    DateTime ReturnDate,
    decimal TotalAmount,
    string Status,
    string? Notes,
    Guid CreatedBy,
    string CreatedByName,
    IReadOnlyList<SupplierReturnItemDto> Items
);

public record SupplierReturnPurchaseOrderLookupDto(
    Guid Id,
    string PoNumber,
    DateTime PoDate,
    Guid SupplierId,
    string SupplierName,
    Guid OutletId,
    string OutletName,
    decimal TotalAmount
);

public record SupplierReturnItemRequest(
    Guid ProductId,
    decimal Qty
);

public record CreateSupplierReturnRequest(
    Guid SupplierId,
    Guid PurchaseOrderId,
    DateTime ReturnDate,
    string? Notes,
    List<SupplierReturnItemRequest> Items
);

public record UpdateSupplierReturnRequest(
    DateTime ReturnDate,
    string? Notes,
    List<SupplierReturnItemRequest> Items
);

public record UpdateSupplierReturnStatusRequest(
    string Status
);

public record SupplierReturnFilters(
    Guid? OutletId,
    Guid? SupplierId,
    Guid? PurchaseOrderId,
    string? Status,
    DateTime? DateFrom,
    DateTime? DateTo,
    int Take
);

public interface ISupplierReturnService
{
    Task<IReadOnlyList<SupplierReturnListItemDto>> GetAsync(SupplierReturnFilters filters, CancellationToken ct = default);
    Task<SupplierReturnDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierReturnPurchaseOrderLookupDto>> GetEligiblePurchaseOrdersAsync(Guid outletId, Guid? supplierId, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierReturnItemDto>> GetEligibleItemsAsync(Guid purchaseOrderId, CancellationToken ct = default);
    Task<SupplierReturnDto> CreateAsync(Guid userId, CreateSupplierReturnRequest request, CancellationToken ct = default);
    Task<SupplierReturnDto> UpdateAsync(Guid userId, Guid id, UpdateSupplierReturnRequest request, CancellationToken ct = default);
    Task<SupplierReturnDto> UpdateStatusAsync(Guid userId, Guid id, UpdateSupplierReturnStatusRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
