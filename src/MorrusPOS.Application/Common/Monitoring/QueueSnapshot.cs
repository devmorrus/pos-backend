namespace MorrusPOS.Application.Common.Monitoring;

public sealed record QueueSnapshot(
    string QueueName,
    long PendingCount,
    long ProcessingCount,
    long FailedCount,
    long DeadLetterCount,
    DateTime? OldestPendingAtUtc,
    DateTime? LastEnqueuedAtUtc,
    DateTime? LastProcessedAtUtc,
    DateTime SampledAtUtc)
{
    public double OldestPendingAgeSeconds => OldestPendingAtUtc.HasValue
        ? Math.Max(0, (SampledAtUtc - OldestPendingAtUtc.Value).TotalSeconds)
        : 0;
}
