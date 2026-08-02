using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Stock;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StockTransfersController : ControllerBase
{
    private readonly IStockTransferService _transferService;
    private readonly ICurrentUserService _currentUser;

    public StockTransfersController(IStockTransferService transferService, ICurrentUserService currentUser)
    {
        _transferService = transferService;
        _currentUser = currentUser;
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
    public async Task<ActionResult<IReadOnlyList<StockTransferDto>>> GetOutgoing(CancellationToken ct)
    {
        var outletId = _currentUser.OutletId;
        if (outletId == null)
        {
            return BadRequest("Data outlet tidak valid.");
        }

        var result = await _transferService.GetOutgoingTransfersAsync(outletId.Value, ct);
        return Ok(result);
    }

    [HttpGet("incoming")]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<IReadOnlyList<StockTransferDto>>> GetIncoming(CancellationToken ct)
    {
        var outletId = _currentUser.OutletId;
        if (outletId == null)
        {
            return BadRequest("Data outlet tidak valid.");
        }

        var result = await _transferService.GetIncomingTransfersAsync(outletId.Value, ct);
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
