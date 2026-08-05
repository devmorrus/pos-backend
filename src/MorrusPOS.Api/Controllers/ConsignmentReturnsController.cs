using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Consignments;

namespace MorrusPOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/consignments/returns")]
public class ConsignmentReturnsController : ControllerBase
{
    private readonly IConsignmentReturnService _returnService;
    private readonly ICurrentUserService _currentUser;

    public ConsignmentReturnsController(IConsignmentReturnService returnService, ICurrentUserService currentUser)
    {
        _returnService = returnService;
        _currentUser = currentUser;
    }

    [HttpGet("{id}")]
    [HasPermission("consignment.manage")]
    public async Task<ActionResult<ConsignmentReturnDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _returnService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpGet]
    [HasPermission("consignment.manage")]
    public async Task<ActionResult<IReadOnlyList<ConsignmentReturnDto>>> GetByOutlet([FromQuery] Guid? outletId, CancellationToken ct)
    {
        var resolvedOutletId = outletId ?? _currentUser.OutletId;
        if (!resolvedOutletId.HasValue)
        {
            return BadRequest("OutletId wajib ditentukan.");
        }

        var result = await _returnService.GetByOutletAsync(resolvedOutletId.Value, ct);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("consignment.manage")]
    public async Task<ActionResult<ConsignmentReturnDto>> Create(CreateConsignmentReturnRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _returnService.CreateAsync(userId.Value, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}/status")]
    [HasPermission("consignment.manage")]
    public async Task<ActionResult<ConsignmentReturnDto>> UpdateStatus(Guid id, UpdateConsignmentReturnStatusRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _returnService.UpdateStatusAsync(userId.Value, id, request, ct);
        return Ok(result);
    }
}
