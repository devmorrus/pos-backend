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
    }

    public async Task LeaveOutletGroup(Guid outletId)
    {
        var businessId = _currentUser.BusinessId
            ?? throw new HubException("Business context tidak tersedia.");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(businessId, outletId));
    }

    internal static string GroupName(Guid businessId, Guid outletId)
        => $"Business_{businessId}_Outlet_{outletId}";
}
