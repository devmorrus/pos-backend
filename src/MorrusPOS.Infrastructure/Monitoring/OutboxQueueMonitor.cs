using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Common.Monitoring;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Monitoring;

public sealed class OutboxQueueMonitor : IQueueMonitor
{
    public const string Queue = "outbox";
    private readonly SystemDbContext _db;

    public OutboxQueueMonitor(SystemDbContext db)
    {
        _db = db;
    }

    public string QueueName => Queue;

    public async Task<QueueSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var sampledAt = DateTime.UtcNow;
        var active = _db.OutboxMessages.AsNoTracking()
            .Where(x => x.Status != OutboxMessageStatus.Processed);
        var aggregate = await active
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Pending = group.LongCount(x => x.Status == OutboxMessageStatus.Pending),
                Processing = group.LongCount(x => x.Status == OutboxMessageStatus.Processing),
                Failed = group.LongCount(x => x.Status == OutboxMessageStatus.Failed),
                DeadLetter = group.LongCount(x => x.Status == OutboxMessageStatus.DeadLetter),
                OldestPending = group.Min(x => x.Status == OutboxMessageStatus.Pending || x.Status == OutboxMessageStatus.Failed
                    ? (DateTime?)x.OccurredAtUtc : null),
            })
            .SingleOrDefaultAsync(ct);
        var lastEnqueued = await _db.OutboxMessages.AsNoTracking()
            .OrderByDescending(x => x.SequenceNo)
            .Select(x => (DateTime?)x.OccurredAtUtc)
            .FirstOrDefaultAsync(ct);
        var lastProcessed = await _db.OutboxMessages.AsNoTracking()
            .Where(x => x.Status == OutboxMessageStatus.Processed)
            .OrderByDescending(x => x.SequenceNo)
            .Select(x => x.ProcessedAtUtc)
            .FirstOrDefaultAsync(ct);

        return new QueueSnapshot(
            Queue,
            aggregate?.Pending ?? 0,
            aggregate?.Processing ?? 0,
            aggregate?.Failed ?? 0,
            aggregate?.DeadLetter ?? 0,
            aggregate?.OldestPending,
            lastEnqueued,
            lastProcessed,
            sampledAt);
    }
}
