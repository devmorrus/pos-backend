using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/inventory/reconciliation")]
[Authorize]
[RequiresCapability(CapabilityCodes.InventoryStock)]
[HasPermission("stock.manage")]
public class StockReconciliationController : ControllerBase
{
    private readonly IStockReconciliationService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly StockLocationAccessResolver _locations;

    public StockReconciliationController(
        IStockReconciliationService service,
        ICurrentUserService currentUser,
        StockLocationAccessResolver locations)
    {
        _service = service;
        _currentUser = currentUser;
        _locations = locations;
    }

    [HttpPost("preview")]
    public async Task<ActionResult<StockReconciliationResultDto>> Preview(
        PreviewStockReconciliationRequest request, CancellationToken ct)
    {
        await _locations.EnsureAsync(request.StockLocationId, ct);
        return Ok(await _service.PreviewAsync(RequireUser(), request.StockLocationId, ct));
    }

    [HttpPost("repair")]
    public async Task<ActionResult<StockReconciliationResultDto>> Repair(
        RepairStockReconciliationRequest request, CancellationToken ct)
    {
        await _locations.EnsureAsync(request.StockLocationId, ct);
        return Ok(await _service.RepairAsync(RequireUser(), request.StockLocationId, request.Reason, ct));
    }

    [HttpGet("runs")]
    public async Task<ActionResult<IReadOnlyList<StockReconciliationRunDto>>> GetRuns(
        [FromQuery] Guid? stockLocationId,
        [FromQuery] Guid? outletId,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var resolvedLocationId = await _locations.ResolveAsync(stockLocationId, outletId, ct);
        return Ok(await _service.GetRunsAsync(resolvedLocationId, take, ct));
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult<StockReconciliationResultDto>> GetRun(Guid id, CancellationToken ct)
    {
        var run = await _service.GetRunAsync(id, ct);
        await _locations.EnsureAsync(run.StockLocationId, ct);
        return Ok(run);
    }

    [HttpGet("runs/{id:guid}/items")]
    public async Task<ActionResult<IReadOnlyList<StockReconciliationMismatchDto>>> GetRunItems(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var run = await _service.GetRunAsync(id, ct);
        await _locations.EnsureAsync(run.StockLocationId, ct);
        return Ok(await _service.GetRunItemsAsync(id, page, pageSize, ct));
    }

    private Guid RequireUser()
        => _currentUser.UserId ?? throw new UnauthorizedAccessException("User tidak valid.");
}
