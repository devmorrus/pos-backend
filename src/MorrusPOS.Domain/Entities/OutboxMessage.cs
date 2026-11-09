using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class OutboxMessageStatus
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Processed = "processed";
    public const string Failed = "failed";
    public const string DeadLetter = "dead_letter";
}

public sealed class OutboxMessage : BaseEntity
{
    public long SequenceNo { get; set; }
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }
    public string EventType { get; set; } = default!;
    public short SchemaVersion { get; set; } = 1;
    public string PayloadJson { get; set; } = default!;
    public string? DeduplicationKey { get; set; }
    public string? CorrelationId { get; set; }
    public string Status { get; set; } = OutboxMessageStatus.Pending;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime AvailableAtUtc { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public Guid? LockId { get; set; }
    public string? LockedBy { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime? DeadLetteredAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public int ManualReplayCount { get; set; }
    public DateTime? LastReplayedAtUtc { get; set; }
    public string? LastReplayedBy { get; set; }
    public string? LastReplayReason { get; set; }
}
