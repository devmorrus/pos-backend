using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MorrusPOS.Api.Hubs;
using MorrusPOS.Application.Common.Events;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Api.Outbox;

public sealed class SignalRStockUpdatedV2EventHandler : IOutboxEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRStockUpdatedV2EventHandler(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public string EventType => OutboxEventTypes.StockUpdated;
    public short SchemaVersion => 2;

    public async Task HandleAsync(OutboxDeliveryMessage message, CancellationToken ct = default)
    {
        StockUpdatedEventV2 payload;
        try
        {
            payload = JsonSerializer.Deserialize<StockUpdatedEventV2>(message.PayloadJson, JsonOptions)
                ?? throw new JsonException("Payload kosong.");
        }
        catch (JsonException exception)
        {
            throw new OutboxPermanentFailureException(
                "OUTBOX_INVALID_PAYLOAD", "Payload stock event V2 tidak valid.", exception);
        }

        if (payload.StockLocationId == Guid.Empty
            || payload.Items.Count == 0
            || (payload.LocationType != StockLocationTypes.Outlet
                && payload.LocationType != StockLocationTypes.Warehouse)
            || message.OutletId != payload.OutletId
            || (payload.LocationType == StockLocationTypes.Outlet
                && payload.OutletId != payload.StockLocationId)
            || (payload.LocationType == StockLocationTypes.Warehouse
                && payload.OutletId is not null))
        {
            throw new OutboxPermanentFailureException(
                "OUTBOX_INVALID_PAYLOAD", "Lokasi atau item stock event V2 tidak valid.");
        }

        var updates = payload.Items.Select(item => new
        {
            item.ProductId,
            item.ProductVariantId,
            Qty = item.QtyOnHand,
        }).ToArray();
        var eventPayload = new
        {
            EventId = message.Id,
            Sequence = message.SequenceNo,
            message.OccurredAtUtc,
            payload.StockLocationId,
            payload.OutletId,
            payload.LocationType,
            IsAbsolute = true,
            Updates = updates,
        };

        await _hubContext.Clients
            .Group(NotificationHub.StockLocationGroupName(message.BusinessId, payload.StockLocationId))
            .SendAsync("ReceiveStockUpdateV2", eventPayload, cancellationToken: ct);

        if (payload.OutletId is { } outletId)
        {
            await _hubContext.Clients
                .Group(NotificationHub.GroupName(message.BusinessId, outletId))
                .SendAsync("ReceiveStockUpdate", new
                {
                    OutletId = outletId,
                    Updates = updates,
                }, cancellationToken: ct);
        }
    }
}
