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
[RequiresCapability(CapabilityCodes.InventoryStock)]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly StockLocationAccessResolver _locations;

    public InventoryController(
        IInventoryService inventoryService,
        StockLocationAccessResolver locations)
    {
        _inventoryService = inventoryService;
        _locations = locations;
    }

    [HttpGet]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<IReadOnlyList<InventoryListItemDto>>> GetByOutlet(
        [FromQuery] Guid? stockLocationId,
        [FromQuery] Guid? outletId,
        [FromQuery] string? search,
        [FromQuery] bool lowStockOnly = false,
        [FromQuery] bool includeZeroStock = true,
        CancellationToken ct = default)
    {
        var resolvedLocationId = await _locations.ResolveAsync(stockLocationId, outletId, ct);

        var result = await _inventoryService.GetByStockLocationAsync(
            resolvedLocationId,
            search,
            lowStockOnly,
            includeZeroStock,
            ct);

        return Ok(result);
    }
}
