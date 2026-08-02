using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Application.Features.Transactions;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;

    public TransactionsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
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
}
