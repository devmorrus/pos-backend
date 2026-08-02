using Microsoft.AspNetCore.SignalR;
using MorrusPOS.Api.Hubs;
using MorrusPOS.Application.Common.Interfaces;

namespace MorrusPOS.Api.Services;

public class PosNotificationService : IPosNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public PosNotificationService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendStockUpdateAsync(Guid outletId, List<StockUpdateItem> updates, CancellationToken ct = default)
    {
        var payload = updates.Select(u => new { u.ProductId, u.Qty }).ToList();
        await _hubContext.Clients.Group($"Outlet_{outletId}")
            .SendAsync("ReceiveStockUpdate", new { OutletId = outletId, Updates = payload }, cancellationToken: ct);
    }
}
