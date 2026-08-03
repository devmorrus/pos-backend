namespace MorrusPOS.Application.Features.Auth;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string Name,
    string Role,
    Guid? OutletId,
    IReadOnlyList<string> Permissions
);

public record RefreshTokenRequest(string RefreshToken);
public record RevokeTokenRequest(string RefreshToken);

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task RevokeTokenAsync(RevokeTokenRequest request, CancellationToken ct = default);
}
