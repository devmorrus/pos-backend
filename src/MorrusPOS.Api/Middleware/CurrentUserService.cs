using System.Security.Claims;
using MorrusPOS.Application.Common.Interfaces;

namespace MorrusPOS.Api.Middleware;

public class CurrentUserService : ICurrentUserService, ITenantContextSetter
{
    private const string TenantOverrideKey = "TenantBusinessId";
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

    public Guid? BusinessId
    {
        get
        {
            // Alur publik/webhook: tenant di-set eksplisit setelah di-resolve.
            if (_accessor.HttpContext?.Items.TryGetValue(TenantOverrideKey, out var overrideValue) == true
                && overrideValue is Guid businessOverride)
                return businessOverride;

            var businessId = User?.FindFirstValue("business_id");
            return Guid.TryParse(businessId, out var id) ? id : null;
        }
    }

    public string? Role => User?.FindFirstValue(ClaimTypes.Role);

    public void SetBusinessOverride(Guid businessId)
    {
        var context = _accessor.HttpContext;
        if (context is not null)
            context.Items[TenantOverrideKey] = businessId;
    }
}
