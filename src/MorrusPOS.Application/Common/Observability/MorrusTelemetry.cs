using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using MorrusPOS.Application.Common.Monitoring;

namespace MorrusPOS.Application.Common.Observability;

public static class MorrusTelemetry
{
    public const string Name = "MorrusPOS";
    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name);

    private static readonly Counter<long> Exceptions = Meter.CreateCounter<long>("morrus.exceptions");
    private static readonly Counter<long> CheckoutAttempts = Meter.CreateCounter<long>("morrus.checkout.attempts");
    private static readonly Histogram<double> CheckoutDuration = Meter.CreateHistogram<double>("morrus.checkout.duration", "ms");
    private static readonly Counter<long> StockIntegrityErrors = Meter.CreateCounter<long>("morrus.stock.integrity_errors");
    private static readonly Counter<long> GoBizWebhooks = Meter.CreateCounter<long>("morrus.gobiz.webhooks");
    private static readonly Histogram<double> QueuePollDuration = Meter.CreateHistogram<double>("morrus.queue.poll_duration", "ms");
    private static readonly ConcurrentDictionary<(string Queue, string Status), long> QueueDepths = new();
    private static readonly ConcurrentDictionary<string, double> QueueOldestAge = new();
    private static readonly ConcurrentDictionary<string, int> QueueMonitorUp = new();
    private static readonly ConcurrentDictionary<string, int> QueueBacklogEvaluation = new();
    private static int _readiness = 1;

    static MorrusTelemetry()
    {
        Meter.CreateObservableGauge("morrus.api.info", () => 1);
        Meter.CreateObservableGauge("morrus.readiness", () => Volatile.Read(ref _readiness));
        Meter.CreateObservableGauge("morrus.queue.depth", () => QueueDepths.Select(item =>
            new Measurement<long>(item.Value,
                new KeyValuePair<string, object?>("queue.name", item.Key.Queue),
                new KeyValuePair<string, object?>("queue.status", item.Key.Status))));
        Meter.CreateObservableGauge("morrus.queue.oldest_age", () => QueueOldestAge.Select(item =>
            new Measurement<double>(item.Value, new KeyValuePair<string, object?>("queue.name", item.Key))), "s");
        Meter.CreateObservableGauge("morrus.queue.monitor_up", () => QueueMonitorUp.Select(item =>
            new Measurement<int>(item.Value, new KeyValuePair<string, object?>("queue.name", item.Key))));
        Meter.CreateObservableGauge("morrus.queue.backlog_evaluation_enabled", () => QueueBacklogEvaluation.Select(item =>
            new Measurement<int>(item.Value, new KeyValuePair<string, object?>("queue.name", item.Key))));
    }

    public static void SetReadiness(bool ready) => Volatile.Write(ref _readiness, ready ? 1 : 0);

    public static void RecordCheckout(string outcome, double durationMs, string channel = "pos")
    {
        var tags = new TagList { { "outcome", outcome }, { "channel", channel } };
        CheckoutAttempts.Add(1, tags);
        CheckoutDuration.Record(durationMs, tags);
    }

    public static void RecordException(string code, int statusCode)
    {
        var tags = new TagList { { "error.code", code }, { "http.response.status_code", statusCode } };
        Exceptions.Add(1, tags);
        if (code.StartsWith("STOCK_", StringComparison.Ordinal))
            StockIntegrityErrors.Add(1, tags);
    }

    public static void RecordGoBizWebhook(string outcome)
        => GoBizWebhooks.Add(1, new TagList { { "outcome", outcome } });

    public static void RecordQueueSnapshot(QueueSnapshot snapshot, bool evaluateBacklog, double durationMs)
    {
        QueueDepths[(snapshot.QueueName, "received")] = snapshot.PendingCount;
        QueueDepths[(snapshot.QueueName, "processing")] = snapshot.ProcessingCount;
        QueueDepths[(snapshot.QueueName, "failed")] = snapshot.FailedCount;
        QueueDepths[(snapshot.QueueName, "dead_letter")] = snapshot.DeadLetterCount;
        QueueOldestAge[snapshot.QueueName] = snapshot.OldestPendingAgeSeconds;
        QueueMonitorUp[snapshot.QueueName] = 1;
        QueueBacklogEvaluation[snapshot.QueueName] = evaluateBacklog ? 1 : 0;
        QueuePollDuration.Record(durationMs, new TagList { { "queue.name", snapshot.QueueName } });
    }

    public static void RecordQueueMonitorFailure(string queueName, bool evaluateBacklog)
    {
        QueueMonitorUp[queueName] = 0;
        QueueBacklogEvaluation[queueName] = evaluateBacklog ? 1 : 0;
    }
}
