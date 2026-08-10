using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Suppliers;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SupplierReturnsController : ControllerBase
{
    private readonly ISupplierReturnService _supplierReturnService;
    private readonly ICurrentUserService _currentUser;

    public SupplierReturnsController(ISupplierReturnService supplierReturnService, ICurrentUserService currentUser)
    {
        _supplierReturnService = supplierReturnService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [HasPermission("supplier_return.manage")]
    public async Task<ActionResult<IReadOnlyList<SupplierReturnListItemDto>>> Get(
        [FromQuery] Guid? outletId,
        [FromQuery] Guid? supplierId,
        [FromQuery] Guid? purchaseOrderId,
        [FromQuery] string? status,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var filters = new SupplierReturnFilters(
            ResolveTargetOutletId(outletId),
            supplierId,
            purchaseOrderId,
            status,
            dateFrom,
            dateTo,
            take);

        var result = await _supplierReturnService.GetAsync(filters, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [HasPermission("supplier_return.manage")]
    public async Task<ActionResult<SupplierReturnDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _supplierReturnService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpGet("eligible-purchase-orders")]
    [HasPermission("supplier_return.manage")]
    public async Task<ActionResult<IReadOnlyList<SupplierReturnPurchaseOrderLookupDto>>> GetEligiblePurchaseOrders(
        [FromQuery] Guid? outletId,
        [FromQuery] Guid? supplierId,
        CancellationToken ct)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        if (resolvedOutletId == null)
        {
            return BadRequest("Pilih outlet terlebih dahulu untuk melihat purchase order yang bisa diretur.");
        }

        var result = await _supplierReturnService.GetEligiblePurchaseOrdersAsync(resolvedOutletId.Value, supplierId, ct);
        return Ok(result);
    }

    [HttpGet("purchase-orders/{purchaseOrderId}/eligible-items")]
    [HasPermission("supplier_return.manage")]
    public async Task<ActionResult<IReadOnlyList<SupplierReturnItemDto>>> GetEligibleItems(Guid purchaseOrderId, CancellationToken ct)
    {
        var result = await _supplierReturnService.GetEligibleItemsAsync(purchaseOrderId, ct);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("supplier_return.manage")]
    public async Task<ActionResult<SupplierReturnDto>> Create(CreateSupplierReturnRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null)
        {
            return BadRequest("User tidak valid.");
        }

        var result = await _supplierReturnService.CreateAsync(_currentUser.UserId.Value, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [HasPermission("supplier_return.manage")]
    public async Task<ActionResult<SupplierReturnDto>> Update(Guid id, UpdateSupplierReturnRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null)
        {
            return BadRequest("User tidak valid.");
        }

        var result = await _supplierReturnService.UpdateAsync(_currentUser.UserId.Value, id, request, ct);
        return Ok(result);
    }

    [HttpPost("{id}/status")]
    [HasPermission("supplier_return.manage")]
    public async Task<ActionResult<SupplierReturnDto>> UpdateStatus(Guid id, UpdateSupplierReturnStatusRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null)
        {
            return BadRequest("User tidak valid.");
        }

        var result = await _supplierReturnService.UpdateStatusAsync(_currentUser.UserId.Value, id, request, ct);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [HasPermission("supplier_return.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _supplierReturnService.DeleteAsync(id, ct);
        return NoContent();
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
