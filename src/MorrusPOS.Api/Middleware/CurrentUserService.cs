using System.Security.Claims;
using MorrusPOS.Application.Common.Interfaces;

namespace MorrusPOS.Api.Middleware;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var sub = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public Guid? OutletId
    {
        get
        {
            var outletId = User?.FindFirstValue("outlet_id");
            return Guid.TryParse(outletId, out var id) ? id : null; // null = Owner, akses semua outlet
        }
    }

    public string? Role => User?.FindFirstValue(ClaimTypes.Role);
}
