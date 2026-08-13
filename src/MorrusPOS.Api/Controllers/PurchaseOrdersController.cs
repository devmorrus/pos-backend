using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Suppliers;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _poService;
    private readonly ICurrentUserService _currentUser;

    public PurchaseOrdersController(IPurchaseOrderService poService, ICurrentUserService currentUser)
    {
        _poService = poService;
        _currentUser = currentUser;
    }

    [HttpGet("{id}")]
    [HasPermission("supplier.manage")]
    public async Task<ActionResult<PurchaseOrderDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _poService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpGet]
    [HasPermission("supplier.manage")]
    public async Task<ActionResult<IReadOnlyList<PurchaseOrderDto>>> GetByOutlet([FromQuery] Guid? outletId, CancellationToken ct)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        if (resolvedOutletId == null)
            return BadRequest("Data outlet tidak valid.");

        var result = await _poService.GetByOutletAsync(resolvedOutletId.Value, ct);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("supplier.manage")]
    public async Task<ActionResult<PurchaseOrderDto>> Create(CreatePurchaseOrderRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return BadRequest("User tidak valid.");

        var result = await _poService.CreateAsync(userId.Value, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}/status")]
    [HasPermission("supplier.manage")]
    public async Task<ActionResult<PurchaseOrderDto>> UpdateStatus(Guid id, UpdatePoStatusRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return BadRequest("User tidak valid.");

        var result = await _poService.UpdateStatusAsync(userId.Value, id, request, ct);
        return Ok(result);
    }

    [HttpPost("{id}/receive")]
    [HasPermission("supplier.manage")]
    public async Task<ActionResult<PurchaseOrderDto>> ReceiveGoods(Guid id, ReceiveGoodsRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return BadRequest("User tidak valid.");

        var result = await _poService.ReceiveGoodsAsync(userId.Value, id, request, ct);
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
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke outlet tersebut.");
        }

        return _currentUser.OutletId;
    }
}
