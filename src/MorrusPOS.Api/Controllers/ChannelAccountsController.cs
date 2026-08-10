using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Channels;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChannelAccountsController : ControllerBase
{
    private readonly IChannelAccountService _channelAccountService;
    private readonly ICurrentUserService _currentUser;

    public ChannelAccountsController(IChannelAccountService channelAccountService, ICurrentUserService currentUser)
    {
        _channelAccountService = channelAccountService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [HasPermission("channel_settlement.manage")]
    public async Task<ActionResult<IReadOnlyList<ChannelAccountDto>>> Get([FromQuery] Guid? outletId, CancellationToken ct)
    {
        var result = await _channelAccountService.GetAsync(ResolveTargetOutletId(outletId), ct);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("channel_settlement.manage")]
    public async Task<ActionResult<ChannelAccountDto>> Create(CreateChannelAccountRequest request, CancellationToken ct)
    {
        var result = await _channelAccountService.CreateAsync(request, ct);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [HasPermission("channel_settlement.manage")]
    public async Task<ActionResult<ChannelAccountDto>> Update(Guid id, UpdateChannelAccountRequest request, CancellationToken ct)
    {
        var result = await _channelAccountService.UpdateAsync(id, request, ct);
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
