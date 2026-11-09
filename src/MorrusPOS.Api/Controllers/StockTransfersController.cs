using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiresCapability(CapabilityCodes.InventoryTransfer)]
public class StockTransfersController : ControllerBase
{
    private readonly IStockTransferService _transferService;
    private readonly ICurrentUserService _currentUser;
    private readonly StockLocationAccessResolver _locations;

    public StockTransfersController(
        IStockTransferService transferService,
        ICurrentUserService currentUser,
        StockLocationAccessResolver locations)
    {
        _transferService = transferService;
        _currentUser = currentUser;
        _locations = locations;
    }

    [HttpGet("{id}")]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<StockTransferDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _transferService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpGet("outgoing")]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<IReadOnlyList<StockTransferDto>>> GetOutgoing(
        [FromQuery] Guid? stockLocationId,
        [FromQuery] Guid? outletId,
        CancellationToken ct)
    {
        var resolvedLocationId = await _locations.ResolveAsync(stockLocationId, outletId, ct);
        var result = await _transferService.GetOutgoingTransfersAsync(resolvedLocationId, ct);
        return Ok(result);
    }

    [HttpGet("incoming")]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<IReadOnlyList<StockTransferDto>>> GetIncoming(
        [FromQuery] Guid? stockLocationId,
        [FromQuery] Guid? outletId,
        CancellationToken ct)
    {
        var resolvedLocationId = await _locations.ResolveAsync(stockLocationId, outletId, ct);
        var result = await _transferService.GetIncomingTransfersAsync(resolvedLocationId, ct);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<StockTransferDto>> Create(CreateStockTransferRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
        {
            return BadRequest("User tidak valid.");
        }

        var result = await _transferService.CreateAsync(userId.Value, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id}/approve")]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<StockTransferDto>> Approve(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
        {
            return BadRequest("User tidak valid.");
        }

        var result = await _transferService.ApproveAsync(userId.Value, id, ct);
        return Ok(result);
    }

    [HttpPost("{id}/reject")]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<StockTransferDto>> Reject(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
        {
            return BadRequest("User tidak valid.");
        }

        var result = await _transferService.RejectAsync(userId.Value, id, ct);
        return Ok(result);
    }
}
