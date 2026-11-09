using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MorrusPOS.Api.Hubs;
using MorrusPOS.Application.Common.Events;
using MorrusPOS.Application.Common.Interfaces;

namespace MorrusPOS.Api.Outbox;

public sealed class SignalRStockUpdatedEventHandler : IOutboxEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRStockUpdatedEventHandler(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public string EventType => OutboxEventTypes.StockUpdated;
    public short SchemaVersion => 1;

    public async Task HandleAsync(OutboxDeliveryMessage message, CancellationToken ct = default)
    {
        StockUpdatedEventV1 payload;
        try
        {
            payload = JsonSerializer.Deserialize<StockUpdatedEventV1>(message.PayloadJson, JsonOptions)
                ?? throw new JsonException("Payload kosong.");
        }
        catch (JsonException exception)
        {
            throw new OutboxPermanentFailureException("OUTBOX_INVALID_PAYLOAD", "Payload stock event tidak valid.", exception);
        }

        if (message.OutletId != payload.OutletId || message.OutletId is null || payload.Items.Count == 0)
            throw new OutboxPermanentFailureException("OUTBOX_INVALID_PAYLOAD", "Outlet atau item stock event tidak valid.");

        var updates = payload.Items.Select(item => new
        {
            item.ProductId,
            item.ProductVariantId,
            Qty = item.QtyOnHand,
        }).ToArray();
        await _hubContext.Clients.Group(NotificationHub.GroupName(message.BusinessId, payload.OutletId))
            .SendAsync("ReceiveStockUpdateV2", new
            {
                EventId = message.Id,
                Sequence = message.SequenceNo,
                message.OccurredAtUtc,
                payload.OutletId,
                IsAbsolute = true,
                Updates = updates,
            }, cancellationToken: ct);
    }
}
