using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Reports;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Owner,Admin,Keuangan,KepalaCabang")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ICurrentUserService _currentUser;

    public ReportsController(IReportService reportService, ICurrentUserService currentUser)
    {
        _reportService = reportService;
        _currentUser = currentUser;
    }

    [HttpGet("profit-loss")]
    public async Task<ActionResult<ProfitLossReportDto>> GetProfitLoss(
        [FromQuery] Guid? outletId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken ct = default)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        var report = await _reportService.GetProfitLossReportAsync(resolvedOutletId, startDate, endDate, ct);
        return Ok(report);
    }

    [HttpGet("profit-loss/export-excel")]
    public async Task<IActionResult> ExportProfitLossExcel(
        [FromQuery] Guid? outletId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken ct = default)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        var response = await _reportService.ExportProfitLossExcelAsync(resolvedOutletId, startDate, endDate, ct);
        return File(response.FileBytes, response.ContentType, response.FileName);
    }

    private Guid? ResolveTargetOutletId(Guid? requestedOutletId)
    {
        if (_currentUser.Role == "Owner" || _currentUser.Role == "Admin" || _currentUser.Role == "Keuangan")
        {
            return requestedOutletId;
        }

        return _currentUser.OutletId;
    }
}
