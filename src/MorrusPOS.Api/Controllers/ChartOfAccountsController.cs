using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/chart-of-accounts")]
[Authorize]
[RequiresCapability(CapabilityCodes.AccountingCore)]
public class ChartOfAccountsController : ControllerBase
{
    private readonly IChartOfAccountService _chartOfAccountService;
    private readonly IAccountingAccountMappingService _mappingService;

    public ChartOfAccountsController(
        IChartOfAccountService chartOfAccountService,
        IAccountingAccountMappingService mappingService)
    {
        _chartOfAccountService = chartOfAccountService;
        _mappingService = mappingService;
    }

    [HttpGet]
    [HasPermission("account.manage")]
    public async Task<ActionResult<IReadOnlyList<ChartOfAccountDto>>> GetAll(CancellationToken ct)
    {
        var result = await _chartOfAccountService.GetAllAsync(ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("account.manage")]
    public async Task<ActionResult<ChartOfAccountDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _chartOfAccountService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("account.manage")]
    public async Task<ActionResult<ChartOfAccountDto>> Create(CreateChartOfAccountRequest request, CancellationToken ct)
    {
        var result = await _chartOfAccountService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("account.manage")]
    public async Task<ActionResult<ChartOfAccountDto>> Update(Guid id, UpdateChartOfAccountRequest request, CancellationToken ct)
    {
        var result = await _chartOfAccountService.UpdateAsync(id, request, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    [HasPermission("account.manage")]
    public async Task<ActionResult<ChartOfAccountDto>> UpdateStatus(Guid id, UpdateChartOfAccountStatusRequest request, CancellationToken ct)
    {
        var result = await _chartOfAccountService.UpdateStatusAsync(id, request, ct);
        return Ok(result);
    }

    [HttpGet("mappings")]
    [HasPermission("account.manage")]
    public async Task<ActionResult<IReadOnlyList<AccountingAccountMappingDto>>> GetMappings(
        [FromQuery] Guid? outletId, CancellationToken ct)
        => Ok(await _mappingService.GetAsync(outletId, ct));

    [HttpPut("mappings/{role}")]
    [HasPermission("account.manage")]
    public async Task<ActionResult<AccountingAccountMappingDto>> UpsertMapping(
        string role, UpsertAccountingAccountMappingRequest request, CancellationToken ct)
        => Ok(await _mappingService.UpsertAsync(role, request, ct));
}
