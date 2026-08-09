using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Auth;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IJwtTokenService _jwt;

    public BillingController(AppDbContext context, ICurrentUserService currentUser, IJwtTokenService jwt)
    {
        _context = context;
        _currentUser = currentUser;
        _jwt = jwt;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var businessId = _currentUser.BusinessId;
        if (businessId == null)
            return BadRequest("Pengguna tidak terasosiasi dengan bisnis mana pun.");

        var business = await _context.Businesses.FindAsync(new object[] { businessId.Value }, ct);
        if (business == null)
            return NotFound("Bisnis tidak ditemukan.");

        var remainingDays = (business.TrialEndDate - DateTime.UtcNow).TotalDays;

        return Ok(new
        {
            businessId = business.Id,
            businessName = business.Name,
            status = business.SubscriptionStatus,
            trialStartDate = business.TrialStartDate,
            trialEndDate = business.TrialEndDate,
            remainingDays = Math.Max(0, (int)Math.Ceiling(remainingDays)),
            selectedPackage = business.SelectedPackage
        });
    }

    [HttpPost("simulate-expiry")]
    public async Task<IActionResult> SimulateExpiry(CancellationToken ct)
    {
        var businessId = _currentUser.BusinessId;
        if (businessId == null)
            return BadRequest("Pengguna tidak terasosiasi dengan bisnis mana pun.");

        var business = await _context.Businesses.FindAsync(new object[] { businessId.Value }, ct);
        if (business == null)
            return NotFound("Bisnis tidak ditemukan.");

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user == null)
            return NotFound("Pengguna tidak ditemukan.");

        // Simulate expiration: set trial end date to yesterday and status to Expired
        business.TrialEndDate = DateTime.UtcNow.AddDays(-1);
        business.SubscriptionStatus = "Expired";
        await _context.SaveChangesAsync(ct);

        // Generate updated JWT to push the new claim state to frontend immediately
        var accessToken = _jwt.GenerateAccessToken(user.Id, user.OutletId, user.Role.Name, business.Id, business.SubscriptionStatus, business.TrialEndDate);
        var refreshTokenString = _jwt.GenerateRefreshToken();

        var permissions = await GetPermissionsForRoleAsync(user.Role.Name, ct);

        return Ok(new LoginResponse(
            accessToken,
            refreshTokenString,
            user.Id,
            user.Name,
            user.Role.Name,
            user.OutletId,
            permissions,
            business.Id,
            business.SubscriptionStatus,
            business.TrialEndDate
        ));
    }

    [HttpPost("reactivate")]
    public async Task<IActionResult> Reactivate(CancellationToken ct)
    {
        var businessId = _currentUser.BusinessId;
        if (businessId == null)
            return BadRequest("Pengguna tidak terasosiasi dengan bisnis mana pun.");

        var business = await _context.Businesses.FindAsync(new object[] { businessId.Value }, ct);
        if (business == null)
            return NotFound("Bisnis tidak ditemukan.");

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user == null)
            return NotFound("Pengguna tidak ditemukan.");

        // Reactivate: extend trial for 30 days from now and set status to Trial
        business.TrialEndDate = DateTime.UtcNow.AddDays(30);
        business.SubscriptionStatus = "Trial";
        await _context.SaveChangesAsync(ct);

        // Generate updated JWT reflecting active state
        var accessToken = _jwt.GenerateAccessToken(user.Id, user.OutletId, user.Role.Name, business.Id, business.SubscriptionStatus, business.TrialEndDate);
        var refreshTokenString = _jwt.GenerateRefreshToken();

        var permissions = await GetPermissionsForRoleAsync(user.Role.Name, ct);

        return Ok(new LoginResponse(
            accessToken,
            refreshTokenString,
            user.Id,
            user.Name,
            user.Role.Name,
            user.OutletId,
            permissions,
            business.Id,
            business.SubscriptionStatus,
            business.TrialEndDate
        ));
    }

    private async Task<IReadOnlyList<string>> GetPermissionsForRoleAsync(string roleName, CancellationToken ct)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Name == roleName, ct);

        if (role is null) return Array.Empty<string>();

        return role.RolePermissions.Select(rp => rp.Permission.Code).ToList();
    }
}
