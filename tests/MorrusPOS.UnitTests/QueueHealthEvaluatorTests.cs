using MorrusPOS.Application.Common.Monitoring;

namespace MorrusPOS.UnitTests;

public class QueueHealthEvaluatorTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, QueueHealthLevel.Healthy)]
    [InlineData(49, 119, 0, 0, QueueHealthLevel.Healthy)]
    [InlineData(50, 60, 0, 0, QueueHealthLevel.Degraded)]
    [InlineData(1, 120, 0, 0, QueueHealthLevel.Degraded)]
    [InlineData(500, 30, 0, 0, QueueHealthLevel.Unhealthy)]
    [InlineData(1, 600, 0, 0, QueueHealthLevel.Unhealthy)]
    [InlineData(0, 0, 1, 0, QueueHealthLevel.Unhealthy)]
    [InlineData(0, 0, 0, 1, QueueHealthLevel.Unhealthy)]
    public void Evaluate_UsesDepthAgeAndFailureThresholds(
        long pending,
        int ageSeconds,
        long failed,
        long deadLetter,
        QueueHealthLevel expected)
    {
        var now = DateTime.UtcNow;
        var snapshot = new QueueSnapshot(
            "gobiz_order_inbox", pending, 0, failed, deadLetter,
            pending > 0 ? now.AddSeconds(-ageSeconds) : null,
            now, null, now);
        var options = new QueueMonitoringOptions { EvaluateBacklog = true };

        Assert.Equal(expected, QueueHealthEvaluator.Evaluate(snapshot, options));
    }

    [Fact]
    public void Evaluate_ReportOnly_DoesNotPageOnExistingBacklog()
    {
        var now = DateTime.UtcNow;
        var snapshot = new QueueSnapshot(
            "gobiz_order_inbox", 10_000, 0, 100, 5,
            now.AddDays(-1), now, null, now);

        Assert.Equal(
            QueueHealthLevel.Healthy,
            QueueHealthEvaluator.Evaluate(snapshot, new QueueMonitoringOptions { EvaluateBacklog = false }));
    }
}
