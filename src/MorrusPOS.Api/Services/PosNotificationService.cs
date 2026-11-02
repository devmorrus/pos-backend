using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Api.Hubs;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Api.Services;

public class PosNotificationService : IPosNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly AppDbContext _db;

    public PosNotificationService(IHubContext<NotificationHub> hubContext, AppDbContext db)
    {
        _hubContext = hubContext;
        _db = db;
    }

    public async Task SendStockUpdateAsync(Guid outletId, List<StockUpdateItem> updates, CancellationToken ct = default)
    {
        var businessId = await _db.Outlets
            .Where(o => o.Id == outletId)
            .Select(o => o.BusinessId)
            .SingleOrDefaultAsync(ct);
        if (businessId == Guid.Empty)
            throw new InvalidOperationException("Outlet tidak tersedia pada business context aktif.");

        var payload = updates.Select(u => new { u.ProductId, u.Qty }).ToList();
        await _hubContext.Clients.Group(NotificationHub.GroupName(businessId, outletId))
            .SendAsync("ReceiveStockUpdate", new { OutletId = outletId, Updates = payload }, cancellationToken: ct);
    }
}
