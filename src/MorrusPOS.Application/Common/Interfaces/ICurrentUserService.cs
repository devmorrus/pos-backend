namespace MorrusPOS.Application.Common.Interfaces;

/// <summary>
/// Abstraksi buat baca identitas user dari HTTP context tanpa Application
/// layer perlu tahu apa itu HttpContext (itu urusan Api layer).
/// Dipakai juga oleh OutletTenantMiddleware & audit logging.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? OutletId { get; } // null = akses semua outlet (role Owner)
    string? Role { get; }
    bool IsAuthenticated { get; }
}

public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userId, Guid? outletId, string role);
    string GenerateRefreshToken();
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
