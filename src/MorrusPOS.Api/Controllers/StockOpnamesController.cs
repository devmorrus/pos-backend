using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Stock;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StockOpnamesController : ControllerBase
{
    private readonly IStockOpnameService _opnameService;
    private readonly ICurrentUserService _currentUser;

    public StockOpnamesController(IStockOpnameService opnameService, ICurrentUserService currentUser)
    {
        _opnameService = opnameService;
        _currentUser = currentUser;
    }

    [HttpGet("{id}")]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<StockOpnameDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _opnameService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpGet]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<IReadOnlyList<StockOpnameDto>>> GetByOutlet(CancellationToken ct)
    {
        var outletId = _currentUser.OutletId;
        if (outletId == null)
        {
            return BadRequest("Data outlet tidak valid.");
        }

        var result = await _opnameService.GetByOutletAsync(outletId.Value, ct);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<StockOpnameDto>> Create(CreateStockOpnameRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
        {
            return BadRequest("User tidak valid.");
        }

        var result = await _opnameService.CreateAsync(userId.Value, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
