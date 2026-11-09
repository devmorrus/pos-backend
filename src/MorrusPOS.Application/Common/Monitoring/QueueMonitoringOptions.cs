namespace MorrusPOS.Application.Common.Monitoring;

public sealed class QueueMonitoringOptions
{
    public const string SectionName = "QueueMonitoring";
    public bool Enabled { get; set; } = true;
    public bool EvaluateBacklog { get; set; }
    public int PollIntervalSeconds { get; set; } = 30;
    public int StaleAfterSeconds { get; set; } = 90;
    public long WarningDepth { get; set; } = 50;
    public long CriticalDepth { get; set; } = 500;
    public int WarningOldestAgeSeconds { get; set; } = 120;
    public int CriticalOldestAgeSeconds { get; set; } = 600;
    public Dictionary<string, QueueMonitoringPolicy> Queues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public QueueMonitoringPolicy GetPolicy(string queueName)
    {
        if (Queues.TryGetValue(queueName, out var configured))
            return configured;
        return new QueueMonitoringPolicy
        {
            EvaluateBacklog = EvaluateBacklog,
            WarningDepth = WarningDepth,
            CriticalDepth = CriticalDepth,
            WarningOldestAgeSeconds = WarningOldestAgeSeconds,
            CriticalOldestAgeSeconds = CriticalOldestAgeSeconds,
        };
    }
}

public sealed class QueueMonitoringPolicy
{
    public bool EvaluateBacklog { get; set; }
    public long WarningDepth { get; set; } = 50;
    public long CriticalDepth { get; set; } = 500;
    public int WarningOldestAgeSeconds { get; set; } = 120;
    public int CriticalOldestAgeSeconds { get; set; } = 600;
    public long WarningFailedCount { get; set; } = 1;
    public long CriticalFailedCount { get; set; } = 1;
    public long CriticalDeadLetterCount { get; set; } = 1;
}

public enum QueueHealthLevel
{
    Healthy,
    Degraded,
    Unhealthy,
}

public static class QueueHealthEvaluator
{
    public static QueueHealthLevel Evaluate(QueueSnapshot snapshot, QueueMonitoringOptions options)
        => Evaluate(snapshot, options.GetPolicy(snapshot.QueueName));

    public static QueueHealthLevel Evaluate(QueueSnapshot snapshot, QueueMonitoringPolicy policy)
    {
        if (!policy.EvaluateBacklog)
            return QueueHealthLevel.Healthy;

        if (snapshot.DeadLetterCount >= policy.CriticalDeadLetterCount ||
            snapshot.FailedCount >= policy.CriticalFailedCount ||
            snapshot.PendingCount >= policy.CriticalDepth ||
            snapshot.OldestPendingAgeSeconds >= policy.CriticalOldestAgeSeconds)
            return QueueHealthLevel.Unhealthy;

        if (snapshot.FailedCount >= policy.WarningFailedCount ||
            snapshot.PendingCount >= policy.WarningDepth ||
            snapshot.OldestPendingAgeSeconds >= policy.WarningOldestAgeSeconds)
            return QueueHealthLevel.Degraded;

        return QueueHealthLevel.Healthy;
    }
}
