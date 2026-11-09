using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Common.Monitoring;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Monitoring;

public sealed class GoBizInboxQueueMonitor : IQueueMonitor
{
    public const string Queue = "gobiz_order_inbox";
    private readonly SystemDbContext _dbContext;

    public GoBizInboxQueueMonitor(SystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string QueueName => Queue;
    public string PendingStatus => "received";

    public async Task<QueueSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var sampledAt = DateTime.UtcNow;
        var aggregate = await _dbContext.GoBizOrderInboxes
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Pending = group.LongCount(item => item.Status == GoBizOrderInboxStatus.Received),
                Processing = group.LongCount(item => item.Status == GoBizOrderInboxStatus.Processing),
                Failed = group.LongCount(item => item.Status == GoBizOrderInboxStatus.Failed),
                DeadLetter = group.LongCount(item => item.Status == GoBizOrderInboxStatus.DeadLetter),
                OldestPending = group.Min(item => item.Status == GoBizOrderInboxStatus.Received
                    ? (DateTime?)item.ReceivedAtUtc : null),
                LastEnqueued = group.Max(item => (DateTime?)item.ReceivedAtUtc),
                LastProcessed = group.Max(item => item.Status == GoBizOrderInboxStatus.Processed
                    ? item.ProcessedAtUtc : null),
            })
            .SingleOrDefaultAsync(ct);

        return new QueueSnapshot(
            QueueName,
            aggregate?.Pending ?? 0,
            aggregate?.Processing ?? 0,
            aggregate?.Failed ?? 0,
            aggregate?.DeadLetter ?? 0,
            aggregate?.OldestPending,
            aggregate?.LastEnqueued,
            aggregate?.LastProcessed,
            sampledAt);
    }
}
