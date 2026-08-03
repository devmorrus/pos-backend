using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Transactions;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Owner,Admin,Kasir")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly ICurrentUserService _currentUser;

    public TransactionsController(ITransactionService transactionService, ICurrentUserService currentUser)
    {
        _transactionService = transactionService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransactionListItemDto>>> GetRecent(
        [FromQuery] Guid? outletId,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        if (resolvedOutletId == null)
        {
            return BadRequest("Pilih outlet terlebih dahulu untuk melihat histori transaksi.");
        }

        var safeTake = Math.Clamp(take, 1, 50);
        var result = await _transactionService.GetRecentByOutletAsync(resolvedOutletId.Value, safeTake, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TransactionDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _transactionService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<TransactionDto>> Checkout(CheckoutRequest request, CancellationToken ct)
    {
        var result = await _transactionService.CheckoutAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id}/void")]
    public async Task<ActionResult<TransactionDto>> Void(Guid id, VoidTransactionRequest request, CancellationToken ct)
    {
        var result = await _transactionService.VoidAsync(id, request, ct);
        return Ok(result);
    }

    [HttpPost("{id}/refund")]
    public async Task<ActionResult<TransactionDto>> Refund(Guid id, RefundTransactionRequest request, CancellationToken ct)
    {
        var result = await _transactionService.RefundAsync(id, request, ct);
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
