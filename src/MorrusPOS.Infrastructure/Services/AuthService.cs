using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Auth;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;

    public AuthService(AppDbContext context, IPasswordHasher hasher, IJwtTokenService jwt)
    {
        _context = context;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Business)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, ct);

        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Email atau password salah.");

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        var businessId = user.BusinessId;
        var subStatus = user.Business?.SubscriptionStatus ?? "Active";
        var trialEndDate = user.Business?.TrialEndDate ?? DateTime.UtcNow.AddYears(100);

        var accessToken = _jwt.GenerateAccessToken(user.Id, user.OutletId, user.Role.Name, businessId, subStatus, trialEndDate);
        var refreshTokenString = _jwt.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenString,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync(ct);

        var permissions = await GetPermissionsForRoleAsync(user.Role.Name, ct);

        return new LoginResponse(
            accessToken,
            refreshTokenString,
            user.Id,
            user.Name,
            user.Role.Name,
            user.OutletId,
            permissions,
            businessId,
            subStatus,
            trialEndDate
        );
    }

    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var storedToken = await _context.RefreshTokens
            .Include(t => t.User)
                .ThenInclude(u => u.Role)
            .Include(t => t.User)
                .ThenInclude(u => u.Business)
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, ct);

        if (storedToken is null || !storedToken.IsActive)
            throw new UnauthorizedAccessException("Refresh token tidak valid atau telah kedaluwarsa.");

        // Revoke current token
        storedToken.RevokedAt = DateTime.UtcNow;

        // Generate new tokens (Rotation)
        var user = storedToken.User;
        var businessId = user.BusinessId;
        var subStatus = user.Business?.SubscriptionStatus ?? "Active";
        var trialEndDate = user.Business?.TrialEndDate ?? DateTime.UtcNow.AddYears(100);

        var newAccessToken = _jwt.GenerateAccessToken(user.Id, user.OutletId, user.Role.Name, businessId, subStatus, trialEndDate);
        var newRefreshTokenString = _jwt.GenerateRefreshToken();

        var newRefreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshTokenString,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync(ct);

        var permissions = await GetPermissionsForRoleAsync(user.Role.Name, ct);

        return new LoginResponse(
            newAccessToken,
            newRefreshTokenString,
            user.Id,
            user.Name,
            user.Role.Name,
            user.OutletId,
            permissions,
            businessId,
            subStatus,
            trialEndDate
        );
    }

    public async Task RevokeTokenAsync(RevokeTokenRequest request, CancellationToken ct = default)
    {
        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, ct);

        if (storedToken is null)
            throw new KeyNotFoundException("Token tidak ditemukan.");

        if (storedToken.IsActive)
        {
            storedToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<LoginResponse> RegisterOwnerAsync(RegisterOwnerRequest request, CancellationToken ct = default)
    {
        // 1. Email uniqueness check
        var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == request.Owner.Email.ToLower(), ct);
        if (emailExists)
        {
            throw new InvalidOperationException("Email sudah terdaftar.");
        }

        // 2. Validate Role
        var ownerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Owner", ct);
        if (ownerRole == null)
        {
            throw new InvalidOperationException("Role 'Owner' tidak ditemukan di database.");
        }

        // 3. Use transaction to guarantee consistency
        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            // Create Business
            var business = new Business
            {
                Id = Guid.NewGuid(),
                Name = request.Business.Name,
                Category = request.Business.Category,
                Phone = request.Business.Phone,
                SubscriptionStatus = "Trial",
                TrialStartDate = DateTime.UtcNow,
                TrialEndDate = DateTime.UtcNow.AddDays(30),
                SelectedPackage = request.Package,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Businesses.Add(business);

            // Create main Outlet
            var outlet = new Outlet
            {
                Id = Guid.NewGuid(),
                BusinessId = business.Id,
                Name = request.Outlet.Name,
                Code = request.Outlet.Code,
                Address = request.Outlet.Address,
                Phone = request.Outlet.Phone,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Outlets.Add(outlet);

            // Create Owner User
            var passwordHash = _hasher.Hash(request.Owner.Password);
            var user = new User
            {
                Id = Guid.NewGuid(),
                BusinessId = business.Id,
                OutletId = null, // Owner has access to all outlets of their business
                RoleId = ownerRole.Id,
                Name = request.Owner.Name,
                Email = request.Owner.Email,
                PasswordHash = passwordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);

            // Update Business OwnerId
            business.OwnerId = user.Id;

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Auto-generate tokens to log them in directly
            var accessToken = _jwt.GenerateAccessToken(user.Id, user.OutletId, ownerRole.Name, business.Id, business.SubscriptionStatus, business.TrialEndDate);
            var refreshTokenString = _jwt.GenerateRefreshToken();

            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshTokenString,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync(ct);

            var permissions = await GetPermissionsForRoleAsync(ownerRole.Name, ct);

            return new LoginResponse(
                accessToken,
                refreshTokenString,
                user.Id,
                user.Name,
                ownerRole.Name,
                user.OutletId,
                permissions,
                business.Id,
                business.SubscriptionStatus,
                business.TrialEndDate
            );
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task<IReadOnlyList<string>> GetPermissionsForRoleAsync(string roleName, CancellationToken ct)
    {
        return await _context.Roles
            .Where(role => role.Name == roleName)
            .SelectMany(role => role.RolePermissions.Select(rolePermission => rolePermission.Permission.Code))
            .Distinct()
            .OrderBy(permission => permission)
            .ToListAsync(ct);
    }
}
