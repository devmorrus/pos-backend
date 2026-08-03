using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Suppliers;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SupplierDebtsController : ControllerBase
{
    private readonly ISupplierDebtService _debtService;
    private readonly ICurrentUserService _currentUser;

    public SupplierDebtsController(ISupplierDebtService debtService, ICurrentUserService currentUser)
    {
        _debtService = debtService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Get all supplier debts, optionally filtered by status (unpaid, partially_paid, paid)
    /// </summary>
    [HttpGet]
    [HasPermission("supplier.manage")]
    public async Task<ActionResult<IReadOnlyList<SupplierDebtDto>>> GetDebts([FromQuery] Guid? outletId, [FromQuery] string? status, CancellationToken ct)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        if (resolvedOutletId == null)
        {
            return BadRequest("Pilih outlet terlebih dahulu untuk melihat utang supplier.");
        }

        var result = await _debtService.GetDebtsAsync(resolvedOutletId.Value, status, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get supplier debt for a specific Purchase Order
    /// </summary>
    [HttpGet("by-po/{purchaseOrderId}")]
    [HasPermission("supplier.manage")]
    public async Task<ActionResult<SupplierDebtDto>> GetDebtByPoId(Guid purchaseOrderId, CancellationToken ct)
    {
        var result = await _debtService.GetDebtByPoIdAsync(purchaseOrderId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get all historical supplier payment records
    /// </summary>
    [HttpGet("payments")]
    [HasPermission("supplier.manage")]
    public async Task<ActionResult<IReadOnlyList<SupplierPaymentDto>>> GetPayments([FromQuery] Guid? outletId, CancellationToken ct)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        if (resolvedOutletId == null)
        {
            return BadRequest("Pilih outlet terlebih dahulu untuk melihat histori pembayaran supplier.");
        }

        var result = await _debtService.GetPaymentsAsync(resolvedOutletId.Value, ct);
        return Ok(result);
    }

    /// <summary>
    /// Record a payment toward a supplier debt linked to a Purchase Order
    /// </summary>
    [HttpPost("pay")]
    [HasPermission("supplier.manage")]
    public async Task<ActionResult<SupplierPaymentDto>> Pay(CreateSupplierPaymentRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return BadRequest("User tidak valid.");

        var result = await _debtService.PayDebtAsync(userId.Value, request, ct);
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
