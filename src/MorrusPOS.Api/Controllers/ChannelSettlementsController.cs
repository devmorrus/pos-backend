using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Channels;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChannelSettlementsController : ControllerBase
{
    private readonly IChannelSettlementService _channelSettlementService;
    private readonly ICurrentUserService _currentUser;

    public ChannelSettlementsController(IChannelSettlementService channelSettlementService, ICurrentUserService currentUser)
    {
        _channelSettlementService = channelSettlementService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [HasPermission("channel_settlement.manage")]
    public async Task<ActionResult<IReadOnlyList<ChannelSettlementListItemDto>>> Get(
        [FromQuery] Guid? outletId,
        [FromQuery] Guid? channelAccountId,
        [FromQuery] string? status,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
    {
        var filters = new ChannelSettlementFilters(
            ResolveTargetOutletId(outletId),
            channelAccountId,
            status,
            dateFrom,
            dateTo);

        var result = await _channelSettlementService.GetAsync(filters, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [HasPermission("channel_settlement.manage")]
    public async Task<ActionResult<ChannelSettlementDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _channelSettlementService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpGet("eligible-transactions")]
    [HasPermission("channel_settlement.manage")]
    public async Task<ActionResult<IReadOnlyList<ChannelSettlementEligibleTransactionDto>>> GetEligibleTransactions(
        [FromQuery] Guid channelAccountId,
        [FromQuery] DateTime periodStartDate,
        [FromQuery] DateTime periodEndDate,
        [FromQuery] Guid? excludeSettlementId,
        CancellationToken ct)
    {
        var result = await _channelSettlementService.GetEligibleTransactionsAsync(
            channelAccountId,
            periodStartDate,
            periodEndDate,
            excludeSettlementId,
            ct);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("channel_settlement.manage")]
    public async Task<ActionResult<ChannelSettlementDto>> Create(CreateChannelSettlementRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null)
        {
            return BadRequest("User tidak valid.");
        }

        var result = await _channelSettlementService.CreateAsync(_currentUser.UserId.Value, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [HasPermission("channel_settlement.manage")]
    public async Task<ActionResult<ChannelSettlementDto>> Update(Guid id, UpdateChannelSettlementRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null)
        {
            return BadRequest("User tidak valid.");
        }

        var result = await _channelSettlementService.UpdateAsync(_currentUser.UserId.Value, id, request, ct);
        return Ok(result);
    }

    [HttpPost("{id}/status")]
    [HasPermission("channel_settlement.manage")]
    public async Task<ActionResult<ChannelSettlementDto>> UpdateStatus(Guid id, UpdateChannelSettlementStatusRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null)
        {
            return BadRequest("User tidak valid.");
        }

        var result = await _channelSettlementService.UpdateStatusAsync(_currentUser.UserId.Value, id, request, ct);
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
