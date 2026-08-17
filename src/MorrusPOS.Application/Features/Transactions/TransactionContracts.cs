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
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    string CustomerType,
    string? ExternalCustomerReference,
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
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    string CustomerType,
    string? ExternalCustomerReference,
    string? ExternalCustomerName,
    string? ExternalCustomerPhone,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal ManualDiscountTotal,
    decimal PromoDiscountTotal,
    decimal VoucherDiscountTotal,
    decimal ServiceChargeTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    string? AppliedVoucherCode,
    string? AppliedPromoName,
    decimal AmountPaid,
    decimal DueAmount,
    DateTime? PaymentDueDate,
    Guid? VoidedBy,
    string? VoidedByName,
    string? VoidedReason,
    DateTime CreatedAt,
    PricingBreakdownDto PricingBreakdown,
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
)
{
    public Guid? ProductVariantId { get; init; }
}

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
)
{
    public Guid? ProductVariantId { get; init; }
}

public record CheckoutItemRequest(
    Guid ProductId,
    decimal Qty,
    decimal UnitPrice,
    decimal DiscountAmount
)
{
    public Guid? ProductVariantId { get; init; }
}

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
    List<PaymentRequest> Payments,
    string? VoucherCode = null,
    string? AppliedPromoCode = null,
    Guid? CustomerId = null,
    string? CustomerPhone = null,
    DateTime? PaymentDueDate = null
);

public record PricingPreviewRequest(
    Guid OutletId,
    string Channel,
    string? VoucherCode,
    string? SelectedPromoCode,
    List<CheckoutItemRequest> Items
);

public record PricingBreakdownDto(
    decimal Subtotal,
    decimal ManualDiscountTotal,
    decimal PromoDiscountTotal,
    decimal VoucherDiscountTotal,
    decimal ServiceChargeTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    AppliedVoucherDto? AppliedVoucher,
    AppliedPromoDto? AppliedPromo,
    IReadOnlyList<PricingLineBreakdownDto> LineBreakdowns
);

public record PricingLineBreakdownDto(
    Guid ProductId,
    string ProductName,
    decimal Qty,
    decimal Subtotal,
    decimal ManualDiscount,
    decimal PromoDiscount,
    decimal VoucherDiscount,
    decimal ServiceCharge,
    decimal Tax,
    decimal LineGrandTotal
)
{
    public Guid? ProductVariantId { get; init; }
}

public record AppliedVoucherDto(
    Guid VoucherId,
    string Code,
    string Name,
    decimal DiscountAmount
);

public record AppliedPromoDto(
    Guid PromoCampaignId,
    string? Code,
    string Name,
    decimal DiscountAmount
);

public record VoidTransactionRequest(
    string Reason
);

public record RefundTransactionItemRequest(
    Guid ProductId,
    decimal Qty
)
{
    public Guid? ProductVariantId { get; init; }
}

public record RefundTransactionRequest(
    string RefundMethod,
    string? Reason,
    List<RefundTransactionItemRequest> Items
);

public record PayDueRequest(
    decimal Amount,
    string Method,
    string? ReferenceNumber
);

public interface ITransactionService
{
    Task<IReadOnlyList<TransactionListItemDto>> GetRecentByOutletAsync(Guid outletId, int take, CancellationToken ct = default);
    Task<TransactionDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PricingBreakdownDto> PreviewPricingAsync(PricingPreviewRequest request, CancellationToken ct = default);
    Task<TransactionDto> CheckoutAsync(CheckoutRequest request, CancellationToken ct = default);
    Task<TransactionDto> VoidAsync(Guid id, VoidTransactionRequest request, CancellationToken ct = default);
    Task<TransactionDto> RefundAsync(Guid id, RefundTransactionRequest request, CancellationToken ct = default);
    Task<TransactionDto> PayDueAsync(Guid transactionId, PayDueRequest request, CancellationToken ct = default);
}
