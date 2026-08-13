using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Reports;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ICurrentUserService _currentUser;

    public ReportsController(IReportService reportService, ICurrentUserService currentUser)
    {
        _reportService = reportService;
        _currentUser = currentUser;
    }

    [HttpGet("cash-flow")]
    [HasPermission("report.view")]
    public async Task<ActionResult<AccountingCashFlowReportDto>> GetCashFlow(
        [FromQuery] AccountingCashFlowReportFilters filters,
        CancellationToken ct = default)
    {
        var resolvedFilters = filters with { OutletId = ResolveTargetOutletId(filters.OutletId) };
        var report = await _reportService.GetCashFlowReportAsync(resolvedFilters, ct);
        return Ok(report);
    }

    [HttpGet("profit-loss")]
    [HasPermission("report.view")]
    public async Task<ActionResult<AccountingProfitLossReportDto>> GetProfitLoss(
        [FromQuery] AccountingProfitLossReportFilters filters,
        CancellationToken ct = default)
    {
        var resolvedFilters = filters with { OutletId = ResolveTargetOutletId(filters.OutletId) };
        var report = await _reportService.GetAccountingProfitLossReportAsync(resolvedFilters, ct);
        return Ok(report);
    }

    [HttpGet("profit-loss/export-excel")]
    [HasPermission("report.view")]
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

    [HttpGet("purchases")]
    [HasPermission("report.view")]
    public async Task<ActionResult<PurchaseRecapReportDto>> GetPurchaseRecap(
        [FromQuery] Guid? outletId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken ct = default)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        var report = await _reportService.GetPurchaseRecapReportAsync(resolvedOutletId, startDate, endDate, ct);
        return Ok(report);
    }

    [HttpGet("purchases/export-excel")]
    [HasPermission("report.view")]
    public async Task<IActionResult> ExportPurchaseRecapExcel(
        [FromQuery] Guid? outletId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken ct = default)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        var response = await _reportService.ExportPurchaseRecapExcelAsync(resolvedOutletId, startDate, endDate, ct);
        return File(response.FileBytes, response.ContentType, response.FileName);
    }

    [HttpGet("sales")]
    [HasPermission("report.view")]
    public async Task<ActionResult<SalesRecapReportDto>> GetSalesRecap(
        [FromQuery] Guid? outletId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken ct = default)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        var report = await _reportService.GetSalesRecapReportAsync(resolvedOutletId, startDate, endDate, ct);
        return Ok(report);
    }

    [HttpGet("sales/export-excel")]
    [HasPermission("report.view")]
    public async Task<IActionResult> ExportSalesRecapExcel(
        [FromQuery] Guid? outletId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken ct = default)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        var response = await _reportService.ExportSalesRecapExcelAsync(resolvedOutletId, startDate, endDate, ct);
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
