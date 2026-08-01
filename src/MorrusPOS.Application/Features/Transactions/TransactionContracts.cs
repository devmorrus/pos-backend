namespace MorrusPOS.Application.Features.Transactions;

public record CreateTransactionItemRequest(Guid ProductId, decimal Qty, decimal DiscountAmount);

public record CreateTransactionPaymentRequest(string Method, decimal Amount, string? ReferenceNumber);

public record CreateTransactionRequest(
    List<CreateTransactionItemRequest> Items,
    List<CreateTransactionPaymentRequest> Payments,
    decimal DiscountTotal,
    decimal TaxTotal
);

public record TransactionDto(
    Guid Id,
    string TransactionNumber,
    string Status,
    decimal GrandTotal,
    DateTime CreatedAt
);

public interface ITransactionService
{
    /// <summary>
    /// Validasi stok cukup, hitung total, insert Transaction + TransactionItems + Payments,
    /// lalu insert StockLedger (movement_type = sale) untuk tiap item — yang otomatis
    /// memicu trigger database untuk update InventoryStock.QtyOnHand.
    /// Wajib dijalankan dalam satu database transaction (atomicity).
    /// </summary>
    Task<TransactionDto> CreateAsync(Guid outletId, Guid userId, CreateTransactionRequest request, CancellationToken ct = default);

    Task<TransactionDto> VoidAsync(Guid transactionId, Guid voidedBy, string reason, CancellationToken ct = default);
}
