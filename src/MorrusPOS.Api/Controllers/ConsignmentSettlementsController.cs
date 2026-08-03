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

    [HttpGet("supplier/{supplierId}")]
    [HasPermission("consignment.manage")]
    public async Task<ActionResult<IReadOnlyList<ConsignmentSettlementDto>>> GetSettlementsBySupplier(Guid supplierId, CancellationToken ct)
    {
        var result = await _settlementService.GetSettlementsBySupplierAsync(supplierId, ct);
        return Ok(result);
    }

    [HttpGet("unpaid-sales/{supplierId}")]
    [HasPermission("consignment.manage")]
    public async Task<ActionResult<IReadOnlyList<ConsignmentSaleDto>>> GetUnpaidSalesBySupplier(Guid supplierId, CancellationToken ct)
    {
        var result = await _settlementService.GetUnpaidSalesBySupplierAsync(supplierId, ct);
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
}
