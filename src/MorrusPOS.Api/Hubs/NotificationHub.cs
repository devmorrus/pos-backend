using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public NotificationHub(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task JoinOutletGroup(Guid outletId)
    {
        var businessId = _currentUser.BusinessId
            ?? throw new HubException("Business context tidak tersedia.");

        if (_currentUser.Role != "Owner" && _currentUser.OutletId != outletId)
            throw new HubException("Outlet tidak dapat diakses.");

        if (!await _db.Outlets.AnyAsync(o => o.Id == outletId && o.BusinessId == businessId))
            throw new HubException("Outlet tidak dapat diakses.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(businessId, outletId));
        await Groups.AddToGroupAsync(Context.ConnectionId, StockLocationGroupName(businessId, outletId));
    }

    public async Task LeaveOutletGroup(Guid outletId)
    {
        var businessId = _currentUser.BusinessId
            ?? throw new HubException("Business context tidak tersedia.");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(businessId, outletId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, StockLocationGroupName(businessId, outletId));
    }

    public async Task JoinStockLocationGroup(Guid stockLocationId)
    {
        var businessId = _currentUser.BusinessId
            ?? throw new HubException("Business context tidak tersedia.");
        var userId = _currentUser.UserId ?? throw new HubException("User context tidak tersedia.");

        var location = await _db.StockLocations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == stockLocationId && x.IsActive);
        if (location is null)
            throw new HubException("Lokasi stok tidak dapat diakses.");

        var hasAccess = _currentUser.Role is "Owner" or "Admin"
            || location.OutletId == _currentUser.OutletId
            || await _db.UserStockLocationAccesses.AnyAsync(
                x => x.UserId == userId && x.StockLocationId == stockLocationId);
        if (!hasAccess)
            throw new HubException("Lokasi stok tidak dapat diakses.");

        await Groups.AddToGroupAsync(Context.ConnectionId, StockLocationGroupName(businessId, stockLocationId));
    }

    public async Task LeaveStockLocationGroup(Guid stockLocationId)
    {
        var businessId = _currentUser.BusinessId
            ?? throw new HubException("Business context tidak tersedia.");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, StockLocationGroupName(businessId, stockLocationId));
    }

    internal static string GroupName(Guid businessId, Guid outletId)
        => $"Business_{businessId}_Outlet_{outletId}";

    internal static string StockLocationGroupName(Guid businessId, Guid stockLocationId)
        => $"Business_{businessId}_StockLocation_{stockLocationId}";
}
