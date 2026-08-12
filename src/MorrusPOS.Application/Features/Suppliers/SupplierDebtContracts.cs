namespace MorrusPOS.Application.Features.Suppliers;

public record SupplierDebtDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    Guid PurchaseOrderId,
    string PoNumber,
    DateTime DueDate,
    decimal Amount,
    decimal PaidAmount,
    decimal RemainingAmount,
    string Status,
    decimal SoldAmount,
    decimal MaxPayableAmount
);

public record SupplierPaymentDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    Guid PurchaseOrderId,
    string PoNumber,
    DateTime PaymentDate,
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber,
    string Status
);

public record CreateSupplierPaymentRequest(
    Guid PurchaseOrderId,
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber
);

public interface ISupplierDebtService
{
    Task<IReadOnlyList<SupplierDebtDto>> GetDebtsAsync(Guid outletId, string? status = null, CancellationToken ct = default);
    Task<SupplierDebtDto> GetDebtByPoIdAsync(Guid purchaseOrderId, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierPaymentDto>> GetPaymentsAsync(Guid outletId, CancellationToken ct = default);
    Task<SupplierPaymentDto> PayDebtAsync(Guid userId, CreateSupplierPaymentRequest request, CancellationToken ct = default);
}
