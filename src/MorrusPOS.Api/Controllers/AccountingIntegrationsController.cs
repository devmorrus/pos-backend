using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/accounting-integrations")]
[RequiresCapability(CapabilityCodes.AccountingIntegration)]
public class AccountingIntegrationsController : ControllerBase
{
    private readonly IAccountingIntegrationService _accountingIntegrationService;
    private readonly IJournalPostingService _journalPostingService;
    private readonly IAccountingReconciliationService _reconciliationService;

    public AccountingIntegrationsController(
        IAccountingIntegrationService accountingIntegrationService,
        IJournalPostingService journalPostingService,
        IAccountingReconciliationService reconciliationService)
    {
        _accountingIntegrationService = accountingIntegrationService;
        _journalPostingService = journalPostingService;
        _reconciliationService = reconciliationService;
    }

    [HttpGet("status/{referenceType}/{referenceIdentifier}")]
    [Microsoft.AspNetCore.Authorization.Authorize]
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

    [HttpGet("journals/{id:guid}")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<ActionResult<JournalEntryDto>> GetJournal(Guid id, CancellationToken ct)
        => Ok(await _journalPostingService.GetByIdAsync(id, ct));

    [HttpGet("journals/by-reference/{referenceType}/{referenceId:guid}")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<ActionResult<JournalEntryDto>> GetJournalByReference(
        string referenceType, Guid referenceId, CancellationToken ct)
    {
        var result = await _journalPostingService.GetByReferenceAsync(referenceType, referenceId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("readiness")]
    [HasPermission("account.manage")]
    public async Task<ActionResult<AccountingReadinessDto>> GetReadiness(CancellationToken ct)
        => Ok(await _reconciliationService.GetReadinessAsync(ct));

    [HttpPost("reconciliation/preview")]
    [HasPermission("account.manage")]
    public async Task<ActionResult<AccountingReconciliationPreviewDto>> PreviewReconciliation(
        AccountingReconciliationRequest request, CancellationToken ct)
        => Ok(await _reconciliationService.PreviewAsync(request, ct));
}
