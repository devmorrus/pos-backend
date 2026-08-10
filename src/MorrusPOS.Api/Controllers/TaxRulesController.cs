using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Features.Pricing;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/tax-rules")]
[Authorize(Roles = "Owner,Admin,Keuangan")]
[HasPermission("pricing.manage")]
public class TaxRulesController : ControllerBase
{
    private readonly IPricingAdminService _pricingAdminService;

    public TaxRulesController(IPricingAdminService pricingAdminService)
    {
        _pricingAdminService = pricingAdminService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaxRuleDto>>> GetAll([FromQuery] Guid? outletId, CancellationToken ct)
        => Ok(await _pricingAdminService.GetTaxRulesAsync(outletId, ct));

    [HttpPost]
    public async Task<ActionResult<TaxRuleDto>> Create(CreateTaxRuleRequest request, CancellationToken ct)
    {
        var result = await _pricingAdminService.CreateTaxRuleAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), new { outletId = result.OutletId }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TaxRuleDto>> Update(Guid id, UpdateTaxRuleRequest request, CancellationToken ct)
        => Ok(await _pricingAdminService.UpdateTaxRuleAsync(id, request, ct));
}
