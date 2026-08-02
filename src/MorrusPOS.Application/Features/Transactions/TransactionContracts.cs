namespace MorrusPOS.Application.Features.Transactions;

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
    DateTime CreatedAt,
    IReadOnlyList<TransactionItemDto> Items,
    IReadOnlyList<PaymentDto> Payments
);

public record TransactionItemDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal Qty,
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

public interface ITransactionService
{
    Task<TransactionDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TransactionDto> CheckoutAsync(CheckoutRequest request, CancellationToken ct = default);
}
