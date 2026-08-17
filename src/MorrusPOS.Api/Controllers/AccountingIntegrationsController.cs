using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Features.Accounting;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/accounting-integrations")]
public class AccountingIntegrationsController : ControllerBase
{
    private readonly IAccountingIntegrationService _accountingIntegrationService;

    public AccountingIntegrationsController(IAccountingIntegrationService accountingIntegrationService)
    {
        _accountingIntegrationService = accountingIntegrationService;
    }

    [HttpGet("status/{referenceType}/{referenceIdentifier}")]
    [HasPermission("account.manage")]
    public async Task<ActionResult<AccountingPostingStatusDto>> GetStatus(string referenceType, string referenceIdentifier, CancellationToken ct)
    {
        var result = await _accountingIntegrationService.GetPostingStatusAsync(referenceType, referenceIdentifier, ct);
        return Ok(result);
    }

    [HttpPost("backfill")]
    [HasPermission("account.manage")]
    public async Task<ActionResult<AccountingBackfillResultDto>> Backfill(AccountingBackfillRequest request, CancellationToken ct)
    {
        var result = await _accountingIntegrationService.BackfillAsync(request, ct);
        return Ok(result);
    }
}
