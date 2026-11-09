using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiresCapability(CapabilityCodes.InventoryOpname)]
public class StockOpnamesController : ControllerBase
{
    private readonly IStockOpnameService _opnameService;
    private readonly ICurrentUserService _currentUser;
    private readonly StockLocationAccessResolver _locations;

    public StockOpnamesController(
        IStockOpnameService opnameService,
        ICurrentUserService currentUser,
        StockLocationAccessResolver locations)
    {
        _opnameService = opnameService;
        _currentUser = currentUser;
        _locations = locations;
    }

    [HttpGet("{id}")]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<StockOpnameDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _opnameService.GetByIdAsync(id, ct);
        await _locations.EnsureAsync(result.StockLocationId, ct);
        return Ok(result);
    }

    [HttpGet]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<IReadOnlyList<StockOpnameDto>>> GetByOutlet(
        [FromQuery] Guid? stockLocationId,
        [FromQuery] Guid? outletId,
        CancellationToken ct)
    {
        var resolvedLocationId = await _locations.ResolveAsync(stockLocationId, outletId, ct);
        var result = await _opnameService.GetByStockLocationAsync(resolvedLocationId, ct);
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

        await _locations.EnsureAsync(request.StockLocationId, ct);
        var result = await _opnameService.CreateAsync(userId.Value, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
