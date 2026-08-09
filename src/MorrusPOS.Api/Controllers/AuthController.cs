using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Application.Features.Auth;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }



    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("register-owner")]
    public async Task<ActionResult<LoginResponse>> RegisterOwner(RegisterOwnerRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterOwnerAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await _authService.RefreshTokenAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(RevokeTokenRequest request, CancellationToken ct)
    {
        await _authService.RevokeTokenAsync(request, ct);
        return NoContent();
    }
}
