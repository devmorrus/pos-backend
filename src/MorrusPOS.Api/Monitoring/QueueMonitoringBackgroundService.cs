using System.Diagnostics;
using Microsoft.Extensions.Options;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Common.Observability;
using MorrusPOS.Application.Common.Monitoring;

namespace MorrusPOS.Api.Monitoring;

public sealed class QueueMonitoringBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly QueueMonitoringState _state;
    private readonly QueueMonitoringOptions _options;
    private readonly ILogger<QueueMonitoringBackgroundService> _logger;

    public QueueMonitoringBackgroundService(
        IServiceScopeFactory scopeFactory,
        QueueMonitoringState state,
        IOptions<QueueMonitoringOptions> options,
        ILogger<QueueMonitoringBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _state = state;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _options.PollIntervalSeconds)));
        do
        {
            await SampleAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SampleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        foreach (var monitor in scope.ServiceProvider.GetServices<IQueueMonitor>())
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var snapshot = await monitor.GetSnapshotAsync(ct);
                stopwatch.Stop();
                var level = QueueHealthEvaluator.Evaluate(snapshot, _options);
                var previous = _state.SetSuccess(snapshot, level);
                MorrusTelemetry.RecordQueueSnapshot(snapshot, _options.EvaluateBacklog, stopwatch.Elapsed.TotalMilliseconds);
                if (previous != level)
                {
                    _logger.Log(
                        level == QueueHealthLevel.Healthy ? LogLevel.Information : LogLevel.Warning,
                        new EventId(2000, "QueueHealthChanged"),
                        "QueueHealthChanged QueueName={QueueName} Previous={Previous} Current={Current} Pending={Pending} Processing={Processing} Failed={Failed} DeadLetter={DeadLetter} OldestPendingAgeSeconds={OldestPendingAgeSeconds} BacklogEvaluationEnabled={BacklogEvaluationEnabled}",
                        snapshot.QueueName, previous?.ToString() ?? "Unknown", level,
                        snapshot.PendingCount, snapshot.ProcessingCount, snapshot.FailedCount,
                        snapshot.DeadLetterCount, Math.Round(snapshot.OldestPendingAgeSeconds, 2),
                        _options.EvaluateBacklog);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _state.SetFailure(monitor.QueueName, exception);
                MorrusTelemetry.RecordQueueMonitorFailure(monitor.QueueName, _options.EvaluateBacklog);
                _logger.LogError(exception, "QueueMonitoringFailed QueueName={QueueName}", monitor.QueueName);
            }
        }
    }
}
