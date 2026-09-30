using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Application.Features.Channels;

namespace MorrusPOS.Api.Controllers;

/// <summary>
/// Opsi B: CRUD kredensial GoBiz per-Business. Hanya Owner / Admin.
/// Client baru tinggal PUT config di sini, tanpa edit appsettings.
/// </summary>
[ApiController]
[Route("api/gobiz/config")]
[Authorize(Roles = "Owner,Admin")]
public class GoBizConfigController : ControllerBase
{
    private readonly IGoBizClientConfigService _service;

    public GoBizConfigController(IGoBizClientConfigService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<GoBizClientConfigDto>> Get([FromQuery] Guid businessId, CancellationToken ct)
    {
        if (businessId == Guid.Empty) return BadRequest(new { message = "businessId wajib diisi." });
        return Ok(await _service.GetAsync(businessId, ct));
    }

    [HttpPut]
    public async Task<ActionResult<GoBizClientConfigDto>> Upsert([FromBody] UpsertGoBizClientConfigRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.UpsertAsync(request, ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("debug")]
    public async Task<ActionResult<GoBizConfigDebugDto>> Debug(
        [FromQuery] Guid? businessId, [FromQuery] Guid? outletId, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.GetDebugAsync(businessId, outletId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
