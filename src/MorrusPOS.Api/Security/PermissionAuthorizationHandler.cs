using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MorrusPOS.Api.Security;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Api.Security;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public PermissionAuthorizationHandler(AppDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(role))
        {
            return;
        }

        var cacheKey = $"role_perms_{role}";
        if (!_cache.TryGetValue(cacheKey, out HashSet<string>? permissions) || permissions == null)
        {
            permissions = await _dbContext.Roles
                .Where(r => r.Name == role)
                .SelectMany(r => r.RolePermissions.Select(rp => rp.Permission.Code))
                .ToHashSetAsync();

            _cache.Set(cacheKey, permissions, TimeSpan.FromMinutes(10));
        }

        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
