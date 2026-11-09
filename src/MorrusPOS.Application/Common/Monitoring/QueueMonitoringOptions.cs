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
    {
        if (!options.EvaluateBacklog)
            return QueueHealthLevel.Healthy;

        if (snapshot.FailedCount > 0 || snapshot.DeadLetterCount > 0 ||
            snapshot.PendingCount >= options.CriticalDepth ||
            snapshot.OldestPendingAgeSeconds >= options.CriticalOldestAgeSeconds)
            return QueueHealthLevel.Unhealthy;

        if (snapshot.PendingCount >= options.WarningDepth ||
            snapshot.OldestPendingAgeSeconds >= options.WarningOldestAgeSeconds)
            return QueueHealthLevel.Degraded;

        return QueueHealthLevel.Healthy;
    }
}
