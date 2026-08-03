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
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, ct);

        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Email atau password salah.");

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        var accessToken = _jwt.GenerateAccessToken(user.Id, user.OutletId, user.Role.Name);
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
            permissions
        );
    }

    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var storedToken = await _context.RefreshTokens
            .Include(t => t.User)
            .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, ct);

        if (storedToken is null || !storedToken.IsActive)
            throw new UnauthorizedAccessException("Refresh token tidak valid atau telah kedaluwarsa.");

        // Revoke current token
        storedToken.RevokedAt = DateTime.UtcNow;

        // Generate new tokens (Rotation)
        var user = storedToken.User;
        var newAccessToken = _jwt.GenerateAccessToken(user.Id, user.OutletId, user.Role.Name);
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
            permissions
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
