namespace MorrusPOS.Application.Common.Interfaces;

public record IdempotencyClaim(
    string Operation,
    Guid Key,
    string RequestHash,
    Guid UserId,
    Guid? OutletId,
    bool Replayed,
    string? ResourceType,
    Guid? ResourceId);

public interface IIdempotencyService
{
    string ComputeRequestHash<T>(T request);
    Task<IdempotencyClaim> ClaimAsync(
        string operation,
        Guid key,
        string requestHash,
        Guid userId,
        Guid? outletId,
        CancellationToken ct = default);
    void Complete(IdempotencyClaim claim, string resourceType, Guid resourceId);
}
