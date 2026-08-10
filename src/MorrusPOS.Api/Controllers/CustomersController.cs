using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Features.Customers;
using MorrusPOS.Application.Features.Transactions;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    [HasPermission("customer.view")]
    public async Task<ActionResult<IReadOnlyList<CustomerListItemDto>>> GetAll(
        [FromQuery] string? q,
        [FromQuery] bool? isMember,
        [FromQuery] bool? isActive,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var result = await _customerService.GetAllAsync(new CustomerListQuery(q, isMember, isActive, dateFrom, dateTo, take), ct);
        return Ok(result);
    }

    [HttpGet("lookup")]
    [HasPermission("customer.view")]
    public async Task<ActionResult<IReadOnlyList<CustomerListItemDto>>> Lookup(
        [FromQuery] string q,
        [FromQuery] int take = 10,
        CancellationToken ct = default)
    {
        var result = await _customerService.LookupAsync(q, take, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("customer.view")]
    public async Task<ActionResult<CustomerDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _customerService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/transactions")]
    [HasPermission("customer.view")]
    public async Task<ActionResult<IReadOnlyList<TransactionListItemDto>>> GetTransactions(
        Guid id,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var result = await _customerService.GetTransactionsAsync(id, take, ct);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("customer.manage")]
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerRequest request, CancellationToken ct = default)
    {
        var result = await _customerService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("customer.manage")]
    public async Task<ActionResult<CustomerDto>> Update(Guid id, UpdateCustomerRequest request, CancellationToken ct = default)
    {
        var result = await _customerService.UpdateAsync(id, request, ct);
        return Ok(result);
    }
}
