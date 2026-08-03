using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Stock;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ICurrentUserService _currentUser;

    public InventoryController(IInventoryService inventoryService, ICurrentUserService currentUser)
    {
        _inventoryService = inventoryService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<IReadOnlyList<InventoryListItemDto>>> GetByOutlet(
        [FromQuery] Guid? outletId,
        [FromQuery] string? search,
        [FromQuery] bool lowStockOnly = false,
        [FromQuery] bool includeZeroStock = true,
        CancellationToken ct = default)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        if (resolvedOutletId == null)
        {
            return BadRequest("Pilih outlet terlebih dahulu untuk melihat inventory.");
        }

        var result = await _inventoryService.GetByOutletAsync(
            resolvedOutletId.Value,
            search,
            lowStockOnly,
            includeZeroStock,
            ct);

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
