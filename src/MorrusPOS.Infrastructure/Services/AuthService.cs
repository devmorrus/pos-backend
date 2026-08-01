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
        var refreshToken = _jwt.GenerateRefreshToken();

        return new LoginResponse(
            accessToken,
            refreshToken,
            user.Id,
            user.Name,
            user.Role.Name,
            user.OutletId
        );
    }
}
