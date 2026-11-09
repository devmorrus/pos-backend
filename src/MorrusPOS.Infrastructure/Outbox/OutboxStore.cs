using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MorrusPOS.Application.Common.Events;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Outbox;

public sealed class OutboxStore : IOutboxStore
{
    private readonly SystemDbContext _db;
    private readonly OutboxOptions _options;

    public OutboxStore(SystemDbContext db, IOptions<OutboxOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<OutboxDeliveryMessage>> ClaimBatchAsync(
        string workerId,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var messages = await _db.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT * FROM outbox_messages
                WHERE "Status" IN ('pending','failed')
                  AND "AvailableAtUtc" <= {now}
                  AND "AttemptCount" < "MaxAttempts"
                ORDER BY "SequenceNo"
                FOR UPDATE SKIP LOCKED
                LIMIT {_options.BatchSize}
                """)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            message.Status = OutboxMessageStatus.Processing;
            message.AttemptCount++;
            message.LockId = Guid.NewGuid();
            message.LockedAtUtc = now;
            message.LockedBy = workerId;
            message.LastAttemptAtUtc = now;
            message.ProcessedAtUtc = null;
            message.DeadLetteredAtUtc = null;
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var result = messages.Select(message => new OutboxDeliveryMessage(
            message.Id,
            message.SequenceNo,
            message.BusinessId,
            message.OutletId,
            message.EventType,
            message.SchemaVersion,
            message.PayloadJson,
            message.CorrelationId,
            message.AttemptCount,
            message.MaxAttempts,
            message.LockId!.Value,
            message.OccurredAtUtc)).ToArray();
        _db.ChangeTracker.Clear();
        return result;
    }

    public async Task<int> RecoverExpiredLeasesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddSeconds(-_options.LeaseSeconds);
        var stale = await _db.OutboxMessages
            .Where(x => x.Status == OutboxMessageStatus.Processing && x.LockedAtUtc < cutoff)
            .OrderBy(x => x.SequenceNo)
            .Take(_options.BatchSize * 4)
            .ToListAsync(ct);

        foreach (var message in stale)
        {
            var exhausted = message.AttemptCount >= message.MaxAttempts;
            message.Status = exhausted ? OutboxMessageStatus.DeadLetter : OutboxMessageStatus.Failed;
            message.AvailableAtUtc = now;
            message.DeadLetteredAtUtc = exhausted ? now : null;
            message.LastErrorCode = "OUTBOX_LEASE_EXPIRED";
            message.LastErrorMessage = "Worker lease expired before delivery was acknowledged.";
            ClearLock(message);
        }

        if (stale.Count > 0)
            await _db.SaveChangesAsync(ct);
        return stale.Count;
    }

    public Task<bool> MarkProcessedAsync(Guid id, Guid lockId, CancellationToken ct = default)
        => UpdateAndCheckAsync(_db.OutboxMessages
            .Where(x => x.Id == id && x.Status == OutboxMessageStatus.Processing && x.LockId == lockId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, OutboxMessageStatus.Processed)
                .SetProperty(x => x.ProcessedAtUtc, DateTime.UtcNow)
                .SetProperty(x => x.LockId, (Guid?)null)
                .SetProperty(x => x.LockedAtUtc, (DateTime?)null)
                .SetProperty(x => x.LockedBy, (string?)null), ct));

    public Task<bool> MarkFailedAsync(
        Guid id,
        Guid lockId,
        string errorCode,
        string errorMessage,
        DateTime? retryAtUtc,
        CancellationToken ct = default)
    {
        var deadLetter = retryAtUtc is null;
        var now = DateTime.UtcNow;
        var code = Truncate(errorCode, 100);
        var message = Truncate(errorMessage, 2000);
        return UpdateAndCheckAsync(_db.OutboxMessages
            .Where(x => x.Id == id && x.Status == OutboxMessageStatus.Processing && x.LockId == lockId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, deadLetter ? OutboxMessageStatus.DeadLetter : OutboxMessageStatus.Failed)
                .SetProperty(x => x.AvailableAtUtc, retryAtUtc ?? now)
                .SetProperty(x => x.DeadLetteredAtUtc, deadLetter ? now : null)
                .SetProperty(x => x.LastErrorCode, code)
                .SetProperty(x => x.LastErrorMessage, message)
                .SetProperty(x => x.LockId, (Guid?)null)
                .SetProperty(x => x.LockedAtUtc, (DateTime?)null)
                .SetProperty(x => x.LockedBy, (string?)null), ct));
    }

    public async Task<int> DeleteProcessedBeforeAsync(DateTime cutoffUtc, int take, CancellationToken ct = default)
    {
        var ids = await _db.OutboxMessages
            .Where(x => x.Status == OutboxMessageStatus.Processed && x.ProcessedAtUtc < cutoffUtc)
            .OrderBy(x => x.SequenceNo)
            .Select(x => x.Id)
            .Take(Math.Clamp(take, 1, 5000))
            .ToArrayAsync(ct);
        return ids.Length == 0
            ? 0
            : await _db.OutboxMessages.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct);
    }

    private static async Task<bool> UpdateAndCheckAsync(Task<int> update)
        => await update == 1;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static void ClearLock(OutboxMessage message)
    {
        message.LockId = null;
        message.LockedAtUtc = null;
        message.LockedBy = null;
    }
}
