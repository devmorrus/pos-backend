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

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConsignmentsController : ControllerBase
{
    private readonly IConsignmentService _consignmentService;
    private readonly ICurrentUserService _currentUser;

    public ConsignmentsController(IConsignmentService consignmentService, ICurrentUserService currentUser)
    {
        _consignmentService = consignmentService;
        _currentUser = currentUser;
    }

    [HttpGet("{id}")]
    [HasPermission("consignment.manage")]
    public async Task<ActionResult<ConsignmentDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _consignmentService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpGet]
    [HasPermission("consignment.manage")]
    public async Task<ActionResult<IReadOnlyList<ConsignmentDto>>> GetByOutlet([FromQuery] Guid? outletId, CancellationToken ct)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        if (resolvedOutletId == null)
        {
            return BadRequest("Pilih outlet terlebih dahulu untuk melihat tanda terima konsinyasi.");
        }

        var result = await _consignmentService.GetByOutletAsync(resolvedOutletId.Value, ct);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("consignment.manage")]
    public async Task<ActionResult<ConsignmentDto>> Create(CreateConsignmentRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
        {
            return BadRequest("User tidak valid.");
        }

        var result = await _consignmentService.CreateAsync(userId.Value, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}/status")]
    [HasPermission("consignment.manage")]
    public async Task<ActionResult<ConsignmentDto>> UpdateStatus(Guid id, UpdateConsignmentStatusRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
        {
            return BadRequest("User tidak valid.");
        }

        var result = await _consignmentService.UpdateStatusAsync(userId.Value, id, request, ct);
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
