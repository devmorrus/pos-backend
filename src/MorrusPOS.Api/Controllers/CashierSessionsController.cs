using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Transactions;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CashierSessionsController : ControllerBase
{
    private readonly ICashierSessionService _sessionService;
    private readonly ICurrentUserService _currentUser;

    public CashierSessionsController(ICashierSessionService sessionService, ICurrentUserService currentUser)
    {
        _sessionService = sessionService;
        _currentUser = currentUser;
    }

    [HttpGet("current")]
    public async Task<ActionResult<CashierSessionDto?>> GetCurrent(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var outletId = _currentUser.OutletId;

        if (userId == null || outletId == null)
        {
            return BadRequest("Sesi kasir hanya dapat diakses oleh user yang terikat dengan Outlet (Kasir/Admin).");
        }

        var result = await _sessionService.GetActiveSessionAsync(userId.Value, outletId.Value, ct);
        return Ok(result);
    }

    [HttpPost("open")]
    public async Task<ActionResult<CashierSessionDto>> Open(OpenSessionRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var outletId = _currentUser.OutletId;

        if (userId == null || outletId == null)
        {
            return BadRequest("Sesi kasir hanya dapat dibuka oleh user yang terikat dengan Outlet (Kasir/Admin).");
        }

        var result = await _sessionService.OpenSessionAsync(userId.Value, outletId.Value, request, ct);
        return Ok(result);
    }

    [HttpPost("close/{id}")]
    public async Task<ActionResult<CashierSessionDto>> Close(Guid id, CloseSessionRequest request, CancellationToken ct)
    {
        var result = await _sessionService.CloseSessionAsync(id, request, ct);
        return Ok(result);
    }
}
