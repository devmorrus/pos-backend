using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MorrusPOS.Api.Monitoring;
using MorrusPOS.Application.Common.Monitoring;

namespace MorrusPOS.Api.Health;

public sealed class QueueBacklogHealthCheck : IHealthCheck
{
    private readonly QueueMonitoringState _state;
    private readonly QueueMonitoringOptions _options;

    public QueueBacklogHealthCheck(
        QueueMonitoringState state,
        IOptions<QueueMonitoringOptions> options)
    {
        _state = state;
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("Queue monitoring dinonaktifkan."));

        var entries = _state.Entries;
        if (entries.Count == 0)
            return Task.FromResult(HealthCheckResult.Degraded("Queue monitor belum menghasilkan snapshot."));

        var now = DateTime.UtcNow;
        var staleAfter = TimeSpan.FromSeconds(Math.Max(15, _options.StaleAfterSeconds));
        bool IsStale(KeyValuePair<string, QueueMonitoringEntry> entry) =>
            !entry.Value.LastSuccessfulSampleAtUtc.HasValue ||
            now - entry.Value.LastSuccessfulSampleAtUtc.Value > staleAfter;
        var level = entries.Max(entry => IsStale(entry)
            ? QueueHealthLevel.Unhealthy
            : entry.Value.HealthLevel);
        var data = entries.ToDictionary(
            entry => entry.Key,
            entry => (object)new
            {
                status = IsStale(entry) ? QueueHealthLevel.Unhealthy.ToString() : entry.Value.HealthLevel.ToString(),
                pending = entry.Value.Snapshot?.PendingCount ?? 0,
                processing = entry.Value.Snapshot?.ProcessingCount ?? 0,
                failed = entry.Value.Snapshot?.FailedCount ?? 0,
                deadLetter = entry.Value.Snapshot?.DeadLetterCount ?? 0,
                oldestPendingAgeSeconds = entry.Value.Snapshot?.OldestPendingAgeSeconds ?? 0,
                lastSuccessfulSampleAtUtc = entry.Value.LastSuccessfulSampleAtUtc,
                monitorStale = IsStale(entry),
                backlogEvaluationEnabled = _options.GetPolicy(entry.Key).EvaluateBacklog,
            });

        return Task.FromResult(level switch
        {
            QueueHealthLevel.Unhealthy => HealthCheckResult.Unhealthy("Queue monitoring tidak sehat.", data: data),
            QueueHealthLevel.Degraded => HealthCheckResult.Degraded("Queue backlog melewati warning threshold.", data: data),
            _ => HealthCheckResult.Healthy("Queue monitoring sehat.", data),
        });
    }
}
