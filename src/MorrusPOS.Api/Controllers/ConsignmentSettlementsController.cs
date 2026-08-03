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
public class ConsignmentSettlementsController : ControllerBase
{
    private readonly IConsignmentSettlementService _settlementService;
    private readonly ICurrentUserService _currentUser;

    public ConsignmentSettlementsController(IConsignmentSettlementService settlementService, ICurrentUserService currentUser)
    {
        _settlementService = settlementService;
        _currentUser = currentUser;
    }

    [HttpGet("{id}")]
    [HasPermission("consignment.manage")]
    public async Task<ActionResult<ConsignmentSettlementDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _settlementService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpGet]
    [HasPermission("consignment.manage")]
    public async Task<ActionResult<IReadOnlyList<ConsignmentSettlementDto>>> GetByOutlet([FromQuery] Guid? outletId, CancellationToken ct)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        if (resolvedOutletId == null)
        {
            return BadRequest("Pilih outlet terlebih dahulu untuk melihat settlement konsinyasi.");
        }

        var result = await _settlementService.GetByOutletAsync(resolvedOutletId.Value, ct);
        return Ok(result);
    }

    [HttpGet("unpaid-sales")]
    [HasPermission("consignment.manage")]
    public async Task<ActionResult<IReadOnlyList<ConsignmentSaleDto>>> GetUnpaidSalesBySupplier(
        [FromQuery] Guid supplierId,
        [FromQuery] Guid? outletId,
        CancellationToken ct)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        if (resolvedOutletId == null)
        {
            return BadRequest("Pilih outlet terlebih dahulu untuk melihat penjualan konsinyasi.");
        }

        var result = await _settlementService.GetUnpaidSalesBySupplierAsync(supplierId, resolvedOutletId.Value, ct);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("consignment.manage")]
    public async Task<ActionResult<ConsignmentSettlementDto>> Create(CreateConsignmentSettlementRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
        {
            return BadRequest("User tidak valid.");
        }

        var result = await _settlementService.CreateSettlementAsync(userId.Value, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}/status")]
    [HasPermission("consignment.manage")]
    public async Task<ActionResult<ConsignmentSettlementDto>> UpdateStatus(Guid id, UpdateConsignmentSettlementStatusRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
        {
            return BadRequest("User tidak valid.");
        }

        var result = await _settlementService.UpdateStatusAsync(userId.Value, id, request, ct);
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
