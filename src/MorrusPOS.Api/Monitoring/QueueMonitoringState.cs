using System.Collections.Concurrent;
using MorrusPOS.Application.Common.Monitoring;

namespace MorrusPOS.Api.Monitoring;

public sealed record QueueMonitoringEntry(
    QueueSnapshot? Snapshot,
    QueueHealthLevel HealthLevel,
    DateTime LastAttemptAtUtc,
    DateTime? LastSuccessfulSampleAtUtc,
    string? LastErrorType);

public sealed class QueueMonitoringState
{
    private readonly ConcurrentDictionary<string, QueueMonitoringEntry> _entries = new();

    public IReadOnlyCollection<KeyValuePair<string, QueueMonitoringEntry>> Entries => _entries.ToArray();

    public QueueHealthLevel? SetSuccess(QueueSnapshot snapshot, QueueHealthLevel level)
    {
        var previous = _entries.TryGetValue(snapshot.QueueName, out var current)
            ? current.HealthLevel : (QueueHealthLevel?)null;
        _entries[snapshot.QueueName] = new QueueMonitoringEntry(
            snapshot, level, snapshot.SampledAtUtc, snapshot.SampledAtUtc, null);
        return previous;
    }

    public QueueHealthLevel? SetFailure(string queueName, Exception exception)
    {
        var now = DateTime.UtcNow;
        var previous = _entries.TryGetValue(queueName, out var current)
            ? current.HealthLevel : (QueueHealthLevel?)null;
        _entries[queueName] = new QueueMonitoringEntry(
            current?.Snapshot, QueueHealthLevel.Unhealthy, now,
            current?.LastSuccessfulSampleAtUtc, exception.GetType().Name);
        return previous;
    }
}
