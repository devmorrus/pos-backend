namespace MorrusPOS.Infrastructure.Services;

public sealed record GoBizOAuthStateContext(
    string State,
    Guid OutletId,
    Guid? BusinessId,
    DateTime ExpiresAtUtc);

public interface IGoBizOAuthStateStore
{
    Task<GoBizOAuthStateContext> CreateAsync(Guid outletId, Guid? businessId, TimeSpan ttl, CancellationToken ct = default);
    Task<GoBizOAuthStateContext?> ValidateAndConsumeAsync(string state, CancellationToken ct = default);
}
