using MorrusPOS.Application.Common.Events;

namespace MorrusPOS.Application.Common.Interfaces;

public interface IOutboxWriter
{
    Task EnqueueAsync(OutboxWriteRequest request, CancellationToken ct = default);
}

public interface IStockEventPublisher
{
    Task EnqueueCurrentStockAsync(
        Guid outletId,
        IReadOnlyCollection<StockEventKey> items,
        string deduplicationKey,
        CancellationToken ct = default);
}

public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxDeliveryMessage>> ClaimBatchAsync(string workerId, CancellationToken ct = default);
    Task<int> RecoverExpiredLeasesAsync(CancellationToken ct = default);
    Task<bool> MarkProcessedAsync(Guid id, Guid lockId, CancellationToken ct = default);
    Task<bool> MarkFailedAsync(
        Guid id,
        Guid lockId,
        string errorCode,
        string errorMessage,
        DateTime? retryAtUtc,
        CancellationToken ct = default);
    Task<int> DeleteProcessedBeforeAsync(DateTime cutoffUtc, int take, CancellationToken ct = default);
}

public interface IOutboxEventHandler
{
    string EventType { get; }
    short SchemaVersion { get; }
    Task HandleAsync(OutboxDeliveryMessage message, CancellationToken ct = default);
}
