using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MorrusPOS.Application.Common.Events;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Common.Observability;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Outbox;

public sealed class OutboxWriter : IOutboxWriter
{
    private readonly AppDbContext _db;
    private readonly OutboxOptions _options;

    public OutboxWriter(AppDbContext db, IOptions<OutboxOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task EnqueueAsync(OutboxWriteRequest request, CancellationToken ct = default)
    {
        if (request.BusinessId == Guid.Empty || string.IsNullOrWhiteSpace(request.EventType) ||
            request.EventType.Length > 150 || request.SchemaVersion <= 0)
            throw new InvalidOperationException("Outbox event wajib memiliki business dan event type.");
        if (request.DeduplicationKey?.Length > 250 || request.CorrelationId?.Length > 64)
            throw new InvalidOperationException("Outbox deduplication atau correlation key terlalu panjang.");
        if (Encoding.UTF8.GetByteCount(request.PayloadJson) > _options.MaxPayloadBytes)
            throw new InvalidOperationException("Payload outbox melebihi batas yang dikonfigurasi.");

        if (!_db.Database.IsRelational())
        {
            if (request.DeduplicationKey is not null && await _db.OutboxMessages.AnyAsync(x =>
                    x.EventType == request.EventType && x.DeduplicationKey == request.DeduplicationKey, ct))
                return;
            _db.OutboxMessages.Add(NewEntity(request));
            MorrusTelemetry.RecordOutboxEnqueued(request.EventType);
            return;
        }

        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Outbox event wajib dibuat di dalam database transaction.");

        var inserted = await _db.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO outbox_messages
                ("Id","BusinessId","OutletId","EventType","SchemaVersion","PayloadJson",
                 "DeduplicationKey","CorrelationId","Status","AttemptCount","MaxAttempts",
                 "OccurredAtUtc","AvailableAtUtc","ManualReplayCount")
            VALUES
                ({{request.Id}},{{request.BusinessId}},{{request.OutletId}},{{request.EventType}},{{request.SchemaVersion}},
                 {{request.PayloadJson}}::jsonb,{{request.DeduplicationKey}},{{request.CorrelationId}},
                 'pending',0,{{_options.MaxAttempts}},{{request.OccurredAtUtc}},{{request.OccurredAtUtc}},0)
            ON CONFLICT ("BusinessId","EventType","DeduplicationKey")
                WHERE "DeduplicationKey" IS NOT NULL
            DO NOTHING;
            """, ct);
        if (inserted > 0) MorrusTelemetry.RecordOutboxEnqueued(request.EventType);
    }

    private OutboxMessage NewEntity(OutboxWriteRequest request) => new()
    {
        Id = request.Id,
        BusinessId = request.BusinessId,
        OutletId = request.OutletId,
        EventType = request.EventType,
        SchemaVersion = request.SchemaVersion,
        PayloadJson = request.PayloadJson,
        DeduplicationKey = request.DeduplicationKey,
        CorrelationId = request.CorrelationId,
        Status = OutboxMessageStatus.Pending,
        MaxAttempts = _options.MaxAttempts,
        OccurredAtUtc = request.OccurredAtUtc,
        AvailableAtUtc = request.OccurredAtUtc,
    };
}
