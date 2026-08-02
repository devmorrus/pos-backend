using Microsoft.AspNetCore.SignalR;

namespace MorrusPOS.Api.Hubs;

public class NotificationHub : Hub
{
    public async Task JoinOutletGroup(string outletId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Outlet_{outletId}");
    }

    public async Task LeaveOutletGroup(string outletId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Outlet_{outletId}");
    }
}
