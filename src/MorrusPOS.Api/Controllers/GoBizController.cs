using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Channels;
using MorrusPOS.Infrastructure.Services;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/gobiz")]
public class GoBizController : ControllerBase
{
    private readonly IGoBizOAuthService _goBizOAuthService;
    private readonly ICurrentUserService _currentUser;
    private readonly string _frontendBaseUrl;
    private readonly IHostEnvironment _hostEnvironment;

    public GoBizController(
        IGoBizOAuthService goBizOAuthService,
        ICurrentUserService currentUser,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        _goBizOAuthService = goBizOAuthService;
        _currentUser = currentUser;
        _hostEnvironment = hostEnvironment;
        _frontendBaseUrl = (configuration["Frontend:Url"] ?? "http://localhost:5173")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "http://localhost:5173";
    }

    [HttpGet("connect")]
    [Authorize]
    public async Task<IActionResult> Connect([FromQuery] Guid outletId, CancellationToken ct)
    {
        var result = await _goBizOAuthService.CreateConnectUrlAsync(ResolveTargetOutletId(outletId), ct);
        return Redirect(result.AuthorizationUrl);
    }

    [HttpPost("connect-url")]
    [Authorize]
    public async Task<ActionResult<GoBizConnectUrlResponse>> CreateConnectUrl([FromBody] GoBizConnectUrlRequest request, CancellationToken ct)
    {
        var result = await _goBizOAuthService.CreateConnectUrlAsync(ResolveTargetOutletId(request.OutletId), ct);
        return Ok(result);
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, [FromQuery(Name = "error_description")] string? errorDescription, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return Redirect(BuildFrontendResultUrl(false, "GOBIZ_AUTHORIZATION_DENIED", string.IsNullOrWhiteSpace(errorDescription) ? "GoBiz membatalkan proses otorisasi." : errorDescription, null));
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return BadRequest(new { code = "GOBIZ_STATE_INVALID", message = "Parameter callback GoBiz tidak lengkap." });
        }

        try
        {
            var result = await _goBizOAuthService.HandleCallbackAsync(code, state, ct);
            return Redirect(BuildFrontendResultUrl(true, null, result.Message, result.OutletId));
        }
        catch (GoBizOAuthException ex)
        {
            return Redirect(BuildFrontendResultUrl(false, ex.Code, ex.Message, null));
        }
        catch (Exception)
        {
            return Redirect(BuildFrontendResultUrl(false, "GOBIZ_TOKEN_EXCHANGE_FAILED", "Gagal memproses callback GoBiz.", null));
        }
    }

    [HttpGet("status")]
    [Authorize]
    public async Task<ActionResult<GoBizConnectionStatusDto>> GetStatus([FromQuery] Guid outletId, CancellationToken ct)
    {
        var result = await _goBizOAuthService.GetStatusAsync(ResolveTargetOutletId(outletId), ct);
        return Ok(result);
    }

    [HttpPost("disconnect")]
    [Authorize]
    public async Task<IActionResult> Disconnect([FromQuery] Guid outletId, CancellationToken ct)
    {
        await _goBizOAuthService.DisconnectAsync(ResolveTargetOutletId(outletId), ct);
        return NoContent();
    }

    [HttpGet("debug/config")]
    [Authorize]
    public ActionResult<GoBizDebugConfigDto> DebugConfig()
    {
        if (!_hostEnvironment.IsDevelopment())
        {
            return NotFound();
        }

        return Ok(_goBizOAuthService.GetDebugConfig());
    }

    private Guid ResolveTargetOutletId(Guid requestedOutletId)
    {
        if (_currentUser.Role == "Owner")
        {
            return requestedOutletId;
        }

        if (_currentUser.OutletId != requestedOutletId)
        {
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke outlet tersebut.");
        }

        return requestedOutletId;
    }

    private string BuildFrontendResultUrl(bool success, string? code, string message, Guid? outletId)
    {
        var builder = new UriBuilder(_frontendBaseUrl)
        {
            Path = "/accounting-integrations"
        };

        var queryParts = new List<string>
        {
            $"integration={Uri.EscapeDataString("gobiz")}",
            $"status={Uri.EscapeDataString(success ? "success" : "error")}",
            $"message={Uri.EscapeDataString(message)}"
        };

        if (!string.IsNullOrWhiteSpace(code))
        {
            queryParts.Add($"code={Uri.EscapeDataString(code)}");
        }

        if (outletId.HasValue)
        {
            queryParts.Add($"outletId={Uri.EscapeDataString(outletId.Value.ToString())}");
        }

        builder.Query = string.Join("&", queryParts);
        return builder.Uri.ToString();
    }
}
