namespace MorrusPOS.Application.Features.Auth;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string Name,
    string Role,
    Guid? OutletId
);

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}
