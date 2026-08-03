using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Transactions;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/cashier-sessions")]
[Authorize(Roles = "Owner,Admin,Kasir,KepalaCabang")]
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
    public async Task<ActionResult<CashierSessionDto?>> GetCurrent([FromQuery] Guid? outletId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var resolvedOutletId = ResolveTargetOutletId(outletId);

        if (userId == null || resolvedOutletId == null)
        {
            return BadRequest("Pilih outlet kerja terlebih dahulu untuk mengakses sesi kasir.");
        }

        var result = await _sessionService.GetActiveSessionAsync(userId.Value, resolvedOutletId.Value, ct);
        return Ok(result);
    }

    [HttpPost("open")]
    public async Task<ActionResult<CashierSessionDto>> Open(OpenSessionRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var outletId = ResolveTargetOutletId(request.OutletId);

        if (userId == null || outletId == null)
        {
            return BadRequest("Pilih outlet kerja terlebih dahulu untuk membuka sesi kasir.");
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

    private Guid? ResolveTargetOutletId(Guid? requestedOutletId)
    {
        if (_currentUser.Role == "Owner")
        {
            return requestedOutletId;
        }

        if (requestedOutletId.HasValue && requestedOutletId != _currentUser.OutletId)
        {
            throw new UnauthorizedAccessException("Anda tidak dapat membuka sesi untuk outlet lain.");
        }

        return _currentUser.OutletId;
    }
}
