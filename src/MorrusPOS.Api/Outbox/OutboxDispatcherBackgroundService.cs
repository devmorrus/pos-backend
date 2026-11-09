using System.Diagnostics;
using Microsoft.Extensions.Options;
using MorrusPOS.Application.Common.Events;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Common.Observability;

namespace MorrusPOS.Api.Outbox;

public sealed class OutboxDispatcherBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxDispatcherBackgroundService> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    private DateTime _lastCleanupUtc = DateTime.MinValue;

    public OutboxDispatcherBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatcherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("OutboxDispatcherDisabled");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await DispatchBatchAsync(stoppingToken);
                if (processed == 0)
                    await Task.Delay(_options.PollIntervalMilliseconds, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "OutboxDispatcherLoopFailed WorkerId={WorkerId}", _workerId);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var handlers = scope.ServiceProvider.GetServices<IOutboxEventHandler>().ToArray();
        var duplicate = handlers.GroupBy(x => (x.EventType, x.SchemaVersion)).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Duplicate outbox handler: {duplicate.Key.EventType} v{duplicate.Key.SchemaVersion}.");
        var handlerMap = handlers.ToDictionary(x => (x.EventType, x.SchemaVersion));

        var recovered = await store.RecoverExpiredLeasesAsync(ct);
        if (recovered > 0)
        {
            MorrusTelemetry.RecordOutboxLeaseRecovery(recovered);
            _logger.LogWarning("OutboxLeasesRecovered Count={Count}", recovered);
        }

        if (DateTime.UtcNow - _lastCleanupUtc >= TimeSpan.FromHours(1))
        {
            var deleted = await store.DeleteProcessedBeforeAsync(
                DateTime.UtcNow.AddDays(-_options.ProcessedRetentionDays), 1000, ct);
            _lastCleanupUtc = DateTime.UtcNow;
            if (deleted > 0)
                _logger.LogInformation("OutboxProcessedMessagesDeleted Count={Count}", deleted);
        }

        var messages = await store.ClaimBatchAsync(_workerId, ct);
        MorrusTelemetry.RecordOutboxClaimed(messages.Count);
        foreach (var message in messages)
            await DispatchOneAsync(store, handlerMap, message, ct);
        return messages.Count;
    }

    private async Task DispatchOneAsync(
        IOutboxStore store,
        IReadOnlyDictionary<(string EventType, short Version), IOutboxEventHandler> handlers,
        OutboxDeliveryMessage message,
        CancellationToken stoppingToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var activity = MorrusTelemetry.ActivitySource.StartActivity("OutboxDispatch", ActivityKind.Consumer);
        activity?.SetTag("messaging.system", "postgresql");
        activity?.SetTag("messaging.operation", "process");
        activity?.SetTag("messaging.destination.name", "outbox");
        activity?.SetTag("messaging.message.id", message.Id);
        activity?.SetTag("morrus.event.type", message.EventType);
        activity?.SetTag("morrus.outbox.attempt", message.AttemptCount);

        try
        {
            if (!handlers.TryGetValue((message.EventType, message.SchemaVersion), out var handler))
                throw new OutboxPermanentFailureException(
                    "OUTBOX_HANDLER_NOT_FOUND",
                    $"Handler tidak tersedia untuk {message.EventType} v{message.SchemaVersion}.");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.HandlerTimeoutSeconds));
            try
            {
                await handler.HandleAsync(message, timeout.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested && timeout.IsCancellationRequested)
            {
                throw new TimeoutException("Outbox handler melewati batas waktu.");
            }

            if (!await store.MarkProcessedAsync(message.Id, message.LockId, stoppingToken))
                _logger.LogWarning("OutboxCompletionIgnored EventId={EventId} LockId={LockId}", message.Id, message.LockId);
            MorrusTelemetry.RecordOutboxDelivery(message.EventType, "success", null, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var permanent = exception is OutboxPermanentFailureException || message.AttemptCount >= message.MaxAttempts;
            var errorCode = exception switch
            {
                OutboxPermanentFailureException known => known.ErrorCode,
                TimeoutException => "OUTBOX_HANDLER_TIMEOUT",
                _ => "OUTBOX_DELIVERY_FAILED",
            };
            DateTime? retryAt = permanent
                ? null
                : DateTime.UtcNow.Add(OutboxRetrySchedule.Calculate(
                    message.AttemptCount,
                    _options,
                    Random.Shared.NextDouble() * 0.2));
            await store.MarkFailedAsync(
                message.Id,
                message.LockId,
                errorCode,
                exception.Message,
                retryAt,
                stoppingToken);
            var outcome = permanent ? "dead_letter" : "retry";
            MorrusTelemetry.RecordOutboxDelivery(message.EventType, outcome, errorCode, stopwatch.Elapsed.TotalMilliseconds);
            _logger.Log(
                permanent ? LogLevel.Error : LogLevel.Warning,
                exception,
                "OutboxDeliveryFailed EventId={EventId} EventType={EventType} Attempt={Attempt} Outcome={Outcome} ErrorCode={ErrorCode} RetryAtUtc={RetryAtUtc}",
                message.Id, message.EventType, message.AttemptCount, outcome, errorCode, retryAt);
        }
    }
}
