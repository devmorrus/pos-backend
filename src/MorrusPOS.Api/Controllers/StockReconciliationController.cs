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

    public StockReconciliationController(
        IStockReconciliationService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpPost("preview")]
    public async Task<ActionResult<StockReconciliationResultDto>> Preview(
        PreviewStockReconciliationRequest request, CancellationToken ct)
        => Ok(await _service.PreviewAsync(RequireUser(), request.OutletId, ct));

    [HttpPost("repair")]
    public async Task<ActionResult<StockReconciliationResultDto>> Repair(
        RepairStockReconciliationRequest request, CancellationToken ct)
        => Ok(await _service.RepairAsync(RequireUser(), request.OutletId, request.Reason, ct));

    [HttpGet("runs")]
    public async Task<ActionResult<IReadOnlyList<StockReconciliationRunDto>>> GetRuns(
        [FromQuery] Guid outletId, [FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await _service.GetRunsAsync(outletId, take, ct));

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult<StockReconciliationResultDto>> GetRun(Guid id, CancellationToken ct)
        => Ok(await _service.GetRunAsync(id, ct));

    [HttpGet("runs/{id:guid}/items")]
    public async Task<ActionResult<IReadOnlyList<StockReconciliationMismatchDto>>> GetRunItems(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _service.GetRunItemsAsync(id, page, pageSize, ct));

    private Guid RequireUser()
        => _currentUser.UserId ?? throw new UnauthorizedAccessException("User tidak valid.");
}
