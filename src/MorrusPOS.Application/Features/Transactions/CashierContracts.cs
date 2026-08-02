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
    string Status
);

public record OpenSessionRequest(decimal OpeningCash);

public record CloseSessionRequest(decimal ActualCash);

public interface ICashierSessionService
{
    Task<CashierSessionDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CashierSessionDto?> GetActiveSessionAsync(Guid userId, Guid outletId, CancellationToken ct = default);
    Task<CashierSessionDto> OpenSessionAsync(Guid userId, Guid outletId, OpenSessionRequest request, CancellationToken ct = default);
    Task<CashierSessionDto> CloseSessionAsync(Guid sessionId, CloseSessionRequest request, CancellationToken ct = default);
}
