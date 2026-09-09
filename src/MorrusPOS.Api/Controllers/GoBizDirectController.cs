using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Application.Features.Channels;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/gobiz/direct")]
[Authorize]
public class GoBizDirectController : ControllerBase
{
    private readonly IGoBizDirectIntegrationService _service;
    private readonly IGoBizDirectAuthService _authService;

    public GoBizDirectController(IGoBizDirectIntegrationService service, IGoBizDirectAuthService authService)
    {
        _service = service;
        _authService = authService;
    }

    [HttpGet("status")]
    public async Task<ActionResult<GoBizDirectStatusDto>> GetStatus([FromQuery] Guid outletId, CancellationToken ct)
        => Ok(await _service.GetStatusAsync(outletId, ct));

    [HttpPost("connect")]
    public async Task<ActionResult<GoBizDirectStatusDto>> Connect([FromBody] GoBizDirectConnectRequest request, CancellationToken ct)
        => Ok(await _service.ConnectAsync(request, ct));

    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect([FromQuery] Guid outletId, CancellationToken ct)
    {
        await _service.DisconnectAsync(outletId, ct);
        return NoContent();
    }

    [HttpGet("token/test")]
    public async Task<ActionResult<GoBizDirectTokenStatusDto>> TestToken([FromQuery] Guid outletId, CancellationToken ct)
    {
        if (outletId == Guid.Empty) return BadRequest(new { message = "outletId wajib diisi." });
        return Ok(await _authService.TestTokenAsync(outletId, ct));
    }

    [HttpGet("catalog/external")]
    public async Task<ActionResult<GoBizExternalCatalogDto>> GetExternalCatalog([FromQuery] Guid outletId, CancellationToken ct)
        => Ok(await _service.GetExternalCatalogAsync(outletId, ct));

    [HttpGet("catalog/preview")]
    public async Task<ActionResult<GoBizCatalogPreviewDto>> PreviewCatalog([FromQuery] Guid outletId, CancellationToken ct)
        => Ok(await _service.PreviewCatalogAsync(outletId, ct));

    [HttpPost("catalog/sync")]
    public async Task<ActionResult<GoBizCatalogSyncResultDto>> SyncCatalog([FromQuery] Guid outletId, CancellationToken ct)
        => Ok(await _service.SyncCatalogAsync(outletId, ct));

    [HttpGet("logs")]
    public async Task<ActionResult<IReadOnlyList<GoBizIntegrationLogDto>>> GetLogs([FromQuery] Guid outletId, [FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await _service.GetLogsAsync(outletId, take, ct));

    [HttpGet("orders")]
    public async Task<ActionResult<IReadOnlyList<GoBizOrderInboxDto>>> GetOrders([FromQuery] Guid outletId, [FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await _service.GetOrderInboxesAsync(outletId, take, ct));
}
