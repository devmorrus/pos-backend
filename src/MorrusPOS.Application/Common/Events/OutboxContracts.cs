namespace MorrusPOS.Application.Common.Events;

public static class OutboxEventTypes
{
    public const string StockUpdated = "pos.stock.updated";
}

public sealed record OutboxWriteRequest(
    Guid Id,
    Guid BusinessId,
    Guid? OutletId,
    string EventType,
    short SchemaVersion,
    string PayloadJson,
    string? DeduplicationKey,
    string? CorrelationId,
    DateTime OccurredAtUtc);

public sealed record OutboxDeliveryMessage(
    Guid Id,
    long SequenceNo,
    Guid BusinessId,
    Guid? OutletId,
    string EventType,
    short SchemaVersion,
    string PayloadJson,
    string? CorrelationId,
    int AttemptCount,
    int MaxAttempts,
    Guid LockId,
    DateTime OccurredAtUtc);

public sealed record StockEventKey(Guid ProductId, Guid? ProductVariantId = null);
public sealed record StockLevelV1(Guid ProductId, Guid? ProductVariantId, decimal QtyOnHand);
public sealed record StockUpdatedEventV1(Guid OutletId, IReadOnlyList<StockLevelV1> Items);
public sealed record StockLevelV2(Guid ProductId, Guid? ProductVariantId, decimal QtyOnHand);
public sealed record StockUpdatedEventV2(
    Guid StockLocationId,
    Guid? OutletId,
    string LocationType,
    IReadOnlyList<StockLevelV2> Items);

public sealed class OutboxPermanentFailureException : Exception
{
    public string ErrorCode { get; }

    public OutboxPermanentFailureException(string errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
