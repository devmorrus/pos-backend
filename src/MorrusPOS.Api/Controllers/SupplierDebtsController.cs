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
    public async Task<ActionResult<IReadOnlyList<SupplierDebtDto>>> GetDebts([FromQuery] string? status, CancellationToken ct)
    {
        var result = await _debtService.GetDebtsAsync(status, ct);
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
    public async Task<ActionResult<IReadOnlyList<SupplierPaymentDto>>> GetPayments(CancellationToken ct)
    {
        var result = await _debtService.GetPaymentsAsync(ct);
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
}
