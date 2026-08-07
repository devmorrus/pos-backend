using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Dashboard;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Owner,Admin,Keuangan,Gudang,Kasir,KepalaCabang")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly ICurrentUserService _currentUser;

    public DashboardController(IDashboardService dashboardService, ICurrentUserService currentUser)
    {
        _dashboardService = dashboardService;
        _currentUser = currentUser;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(
        [FromQuery] Guid? outletId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken ct = default)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        
        var summary = await _dashboardService.GetSummaryAsync(resolvedOutletId, startDate, endDate, ct);
        return Ok(summary);
    }

    [HttpGet("role-summary")]
    public async Task<ActionResult<RoleDashboardDto>> GetRoleSummary(
        [FromQuery] Guid? outletId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken ct = default)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        var summary = await _dashboardService.GetRoleSummaryAsync(
            _currentUser.Role ?? "Kasir",
            _currentUser.UserId ?? Guid.Empty,
            resolvedOutletId,
            startDate,
            endDate,
            ct);
        return Ok(summary);
    }

    private Guid? ResolveTargetOutletId(Guid? requestedOutletId)
    {
        if (_currentUser.Role == "Owner" || _currentUser.Role == "Admin" || _currentUser.Role == "Keuangan")
        {
            return requestedOutletId;
        }

        // KepalaCabang (and anyone else who is authorized) is strictly locked to their own outlet
        return _currentUser.OutletId;
    }
}
