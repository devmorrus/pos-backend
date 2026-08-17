using System.Collections.Generic;

namespace MorrusPOS.Application.Features.Transactions;

public record CashierSessionDto(
    Guid Id,
    Guid OutletId,
    string OutletName,
    Guid UserId,
    string UserName,
    DateTime OpeningTime,
    DateTime? ClosingTime,
    decimal OpeningCash,
    decimal ExpectedCash,
    decimal? ActualCash,
    decimal? Variance,
    string Status,
    decimal TotalCashReceived,
    decimal TotalPettyCashExpenses,
    IReadOnlyDictionary<string, decimal> PaymentsSummary
);

public record OpenSessionRequest(decimal OpeningCash, Guid? OutletId = null);

public record CloseSessionRequest(decimal ActualCash);

public record PettyCashExpenseDto(
    Guid Id,
    Guid OutletId,
    string OutletName,
    Guid CashierSessionId,
    decimal Amount,
    string Description,
    string Category,
    Guid ProcessedBy,
    string ProcessedByName,
    DateTime CreatedAt
);

public record CreatePettyCashRequest(
    decimal Amount,
    string Description,
    string Category
);

public interface ICashierSessionService
{
    Task<CashierSessionDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CashierSessionDto?> GetActiveSessionAsync(Guid userId, Guid outletId, CancellationToken ct = default);
    Task<CashierSessionDto> OpenSessionAsync(Guid userId, Guid outletId, OpenSessionRequest request, CancellationToken ct = default);
    Task<CashierSessionDto> CloseSessionAsync(Guid sessionId, CloseSessionRequest request, CancellationToken ct = default);
    Task<PettyCashExpenseDto> RecordPettyCashAsync(Guid sessionId, CreatePettyCashRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<PettyCashExpenseDto>> GetPettyCashExpensesAsync(Guid sessionId, CancellationToken ct = default);
}
