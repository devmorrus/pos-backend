using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Application.Features.Channels;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/gobiz/webhooks")]
public class GoBizWebhookController : ControllerBase
{
    private readonly IGoBizOrderWebhookService _service;

    public GoBizWebhookController(IGoBizOrderWebhookService service)
    {
        _service = service;
    }

    [HttpPost("orders")]
    [AllowAnonymous]
    public async Task<IActionResult> Orders([FromBody] GoBizWebhookOrderRequest request, CancellationToken ct)
    {
        var rawBody = HttpContext.Items.TryGetValue("RawRequestBody", out var raw) ? raw as string : null;
        var headers = Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        await _service.ProcessWebhookAsync(rawBody ?? System.Text.Json.JsonSerializer.Serialize(request), headers, ct);
        return Ok(new { success = true });
    }
}
