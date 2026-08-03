namespace MorrusPOS.Application.Features.Transactions;

public record TransactionListItemDto(
    Guid Id,
    string TransactionNumber,
    Guid OutletId,
    string OutletName,
    Guid UserId,
    string UserName,
    decimal GrandTotal,
    string Status,
    string Channel,
    DateTime CreatedAt,
    string PaymentSummary
);

public record TransactionDto(
    Guid Id,
    string TransactionNumber,
    Guid OutletId,
    string OutletName,
    Guid UserId,
    string UserName,
    Guid? CashierSessionId,
    string Channel,
    string Status,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    Guid? VoidedBy,
    string? VoidedByName,
    string? VoidedReason,
    DateTime CreatedAt,
    IReadOnlyList<TransactionItemDto> Items,
    IReadOnlyList<PaymentDto> Payments,
    IReadOnlyList<TransactionReturnDto> Returns
);

public record TransactionItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal Qty,
    decimal ReturnedQty,
    decimal RemainingQty,
    decimal UnitPrice,
    decimal UnitCost,
    decimal DiscountAmount,
    decimal LineTotal
);

public record PaymentDto(
    string Method,
    decimal Amount,
    string? ReferenceNumber,
    DateTime CreatedAt
);

public record TransactionReturnDto(
    Guid Id,
    Guid TransactionItemId,
    Guid ProductId,
    string ProductName,
    decimal Qty,
    string? Reason,
    string RefundMethod,
    Guid ProcessedBy,
    string ProcessedByName,
    DateTime CreatedAt
);

public record CheckoutItemRequest(
    Guid ProductId,
    decimal Qty,
    decimal UnitPrice,
    decimal DiscountAmount
);

public record PaymentRequest(
    string Method,
    decimal Amount,
    string? ReferenceNumber
);

public record CheckoutRequest(
    Guid Id,
    Guid OutletId,
    Guid CashierSessionId,
    string Channel,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    List<CheckoutItemRequest> Items,
    List<PaymentRequest> Payments
);

public record VoidTransactionRequest(
    string Reason
);

public record RefundTransactionItemRequest(
    Guid ProductId,
    decimal Qty
);

public record RefundTransactionRequest(
    string RefundMethod,
    string? Reason,
    List<RefundTransactionItemRequest> Items
);

public interface ITransactionService
{
    Task<IReadOnlyList<TransactionListItemDto>> GetRecentByOutletAsync(Guid outletId, int take, CancellationToken ct = default);
    Task<TransactionDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TransactionDto> CheckoutAsync(CheckoutRequest request, CancellationToken ct = default);
    Task<TransactionDto> VoidAsync(Guid id, VoidTransactionRequest request, CancellationToken ct = default);
    Task<TransactionDto> RefundAsync(Guid id, RefundTransactionRequest request, CancellationToken ct = default);
}
